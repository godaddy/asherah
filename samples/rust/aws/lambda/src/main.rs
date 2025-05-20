use appencryption::crypto::aes256gcm::Aes256GcmCrypto;
use appencryption::DataRowRecord;
use appencryption::kms::aws::AwsKmsClientBuilder;
use appencryption::metastore::DynamoDbMetastore;
use appencryption::metrics::{set_metrics_provider, MetricsProvider};
use appencryption::policy::CryptoPolicyBuilder;
use appencryption::session::{Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;

use lambda_runtime::{service_fn, Error, LambdaEvent};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fmt;
use std::sync::{Arc, Mutex, OnceLock};
use once_cell::sync::Lazy;
use regex::Regex;
use tokio::time::{Duration, Instant};
use anyhow::anyhow;
use metrics::{counter, gauge};
use std::error::Error as StdError;
use procfs::Meminfo;

static INVOCATION_COUNTER: Lazy<metrics::Counter> = Lazy::new(|| {
    metrics::register_counter!("asherah.samples.lambda_rust.invocations")
});

static FACTORY: OnceLock<Arc<SessionFactory>> = OnceLock::new();
static SECRET_FACTORY: Lazy<Arc<DefaultSecretFactory>> = Lazy::new(|| {
    Arc::new(DefaultSecretFactory::new())
});

/// Input event structure for the Lambda function
#[derive(Deserialize, Debug)]
struct MyEvent {
    name: String,
    partition: String,
    #[serde(default)]
    payload: Option<Vec<u8>>,
    #[serde(default)]
    drr: Option<DataRowRecord>,
}

/// Output response structure for the Lambda function
#[derive(Serialize, Debug)]
struct MyResponse {
    #[serde(skip_serializing_if = "Option::is_none")]
    plain_text: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    drr: Option<DataRowRecord>,
    metrics: HashMap<String, f64>,
}

/// Simple metrics provider implementation for Asherah
struct LambdaMetricsProvider {
    metrics: Arc<Mutex<HashMap<String, f64>>>,
}

impl LambdaMetricsProvider {
    fn new() -> Self {
        Self {
            metrics: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    fn get_metrics(&self) -> HashMap<String, f64> {
        let metrics = self.metrics.lock().unwrap();
        metrics.clone()
    }
}

impl MetricsProvider for LambdaMetricsProvider {
    fn increment_counter(&self, name: &str, amount: u64) {
        let mut metrics = self.metrics.lock().unwrap();
        *metrics.entry(name.to_string()).or_insert(0.0) += amount as f64;
        
        // Also update the global metrics for Prometheus if needed
        counter!(name.parse().unwrap(), amount as u64);
    }

    fn record_timing(&self, name: &str, duration: std::time::Duration) {
        let duration_ms = duration.as_secs_f64() * 1000.0;
        let mut metrics = self.metrics.lock().unwrap();
        
        // Record last timing
        metrics.insert(format!("{}_last", name), duration_ms);
        
        // Update count
        let count = metrics.entry(format!("{}_count", name)).or_insert(0.0);
        *count += 1.0;
        
        // Update average
        let curr_avg = metrics.entry(format!("{}_avg", name)).or_insert(duration_ms);
        *curr_avg = (*curr_avg * (*count - 1.0) + duration_ms) / *count;
    }
}

/// Initialize the SessionFactory if it hasn't been initialized yet
async fn init_factory() -> Result<Arc<SessionFactory>, Error> {
    if let Some(factory) = FACTORY.get() {
        tracing::info!("Factory already initialized. Reusing...");
        return Ok(factory.clone());
    }

    print_rlimit()?;

    // Setup crypto policy
    let policy = CryptoPolicyBuilder::new()
        .with_session_cache()
        .with_session_cache_max_size(10)
        .build();

    tracing::info!("Creating metastore");
    let metastore = create_metastore().await?;

    let crypto = Arc::new(Aes256GcmCrypto::default());

    tracing::info!("Creating KMS");
    let kms = create_kms(crypto).await?;

    tracing::info!("Creating session factory");
    let factory = SessionFactory::new(
        "asherah-samples",
        "lambda-sample-app",
        policy,
        kms,
        metastore,
        SECRET_FACTORY.clone(),
    );

    let factory_arc = Arc::new(factory);
    if let Err(_) = FACTORY.set(factory_arc.clone()) {
        tracing::warn!("Factory was already set in a race condition");
    }
    
    Ok(factory_arc)
}

/// Reset the factory (used after panic recovery)
async fn reset_factory() -> Result<(), Error> {
    if let Some(_) = FACTORY.get() {
        // Can't actually close/reset the factory with OnceLock, so we'll just log that we would
        tracing::info!("Would reset factory here");
        
        // In a real implementation, you would need to use a different approach
        // that allows resetting the factory, like storing it in an Arc<Mutex<Option<SessionFactory>>>
    }
    
    // Clear metrics
    //securememory::metrics::AllocCounter.clear();
    //securememory::metrics::InUseCounter.clear();
    
    // Initialize factory again
    init_factory().await?;
    
    Ok(())
}

/// Create a new DynamoDB metastore for Asherah
async fn create_metastore() -> Result<Arc<dyn appencryption::Metastore>, Error> {
    let table_name = std::env::var("ASHERAH_METASTORE_TABLE_NAME")
        .unwrap_or_else(|_| "encryption_key".to_string());

    let config = aws_config::from_env().load().await;
    let client = aws_sdk_dynamodb::Client::new(&config);
    
    // Create the DynamoDB metastore
    let metastore = DynamoDbMetastore::new(&client, &table_name).await
        .map_err(|e| Error::from(format!("Failed to create DynamoDB metastore: {}", e)))?;
    
    Ok(Arc::new(metastore))
}

/// Create AWS KMS client using the environment-provided key
async fn create_kms(crypto: Arc<dyn appencryption::Crypto>) -> Result<Arc<dyn appencryption::KeyManagementService>, Error> {
    let region = std::env::var("AWS_REGION")
        .map_err(|_| Error::from("AWS_REGION environment variable not set"))?;
    
    let key_arn = std::env::var("ASHERAH_KMS_KEY_ARN")
        .map_err(|_| Error::from("ASHERAH_KMS_KEY_ARN environment variable not set"))?;

    let kms = AwsKmsClientBuilder::new()
        .with_region(&region)
        .with_key_id(&key_arn)
        .build()
        .await
        .map_err(|e| Error::from(format!("Failed to create AWS KMS client: {}", e)))?;
    
    Ok(Arc::new(kms))
}

/// Error wrapper for recovered errors from panics
#[derive(Debug)]
struct RecoveredError {
    error: Box<dyn StdError + Send + Sync>,
}

impl RecoveredError {
    fn is_retryable(&self) -> bool {
        // The error string pattern we're looking for that indicates we ran out of memory
        let pattern = r"<memcall> could not acquire lock on 0x[0-9a-f].*?, limit reached\? \[Err: cannot allocate memory\]";
        let mlock_err = Regex::new(pattern).unwrap();
        
        mlock_err.is_match(&self.error.to_string())
    }
}

impl fmt::Display for RecoveredError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.error)
    }
}

impl StdError for RecoveredError {
    fn source(&self) -> Option<&(dyn StdError + 'static)> {
        self.error.source()
    }
}

/// Print the current memory lock resource limits
fn print_rlimit() -> Result<(), Error> {
    // RLIMIT_MEMLOCK constant value for Linux/Unix
    #[cfg(target_family = "unix")]
    let rlimit_memlock: libc::c_int = libc::RLIMIT_MEMLOCK;
    
    #[cfg(target_family = "unix")]
    unsafe {
        let mut rlim: libc::rlimit = std::mem::zeroed();
        if libc::getrlimit(rlimit_memlock, &mut rlim) == 0 {
            tracing::info!("MEMLOCK RLIMIT = {}:{}", rlim.rlim_cur, rlim.rlim_max);
        } else {
            return Err(Error::from("Failed to get memory lock resource limits"));
        }
    }
    
    #[cfg(not(target_family = "unix"))]
    tracing::info!("MEMLOCK RLIMIT not available on this platform");
    
    Ok(())
}

/// Print current memory metrics
fn print_metrics(msg: &str) {
    // On Linux, we can get memory info from procfs
    #[cfg(target_os = "linux")]
    if let Ok(meminfo) = Meminfo::new() {
        tracing::info!(
            "metrics: {}, locked.memory: {}",
            msg,
            meminfo.mlocked.unwrap_or(0)
        );
    }
    
    #[cfg(not(target_os = "linux"))]
    tracing::info!("metrics: {}", msg);
}

/// Try to handle the Lambda event with panic recovery
async fn try_handle(
    ctx: &lambda_runtime::Context, 
    event: MyEvent,
    metrics_provider: Arc<LambdaMetricsProvider>,
) -> Result<MyResponse, Box<dyn StdError + Send + Sync>> {
    // Attempt to handle the event, catching any panics
    let result = tokio::task::spawn_blocking(move || {
        tokio::runtime::Handle::current().block_on(async {
            let result: Result<MyResponse, Error> = match (&event.payload, &event.drr) {
                (Some(payload), _) => handle_encrypt(&ctx, event, metrics_provider).await,
                (_, Some(_)) => handle_decrypt(&ctx, event, metrics_provider).await,
                _ => Err(Error::from("Event must contain a Payload (for encryption) or DRR (for decryption)")),
            };
            result
        })
    }).await;
    
    match result {
        // Task completed without panicking
        Ok(Ok(response)) => Ok(response),
        Ok(Err(e)) => Err(Box::new(e)),
        
        // Task panicked
        Err(e) => {
            if e.is_panic() {
                // Try to recover the panic payload
                if let Ok(any) = e.into_panic() {
                    // If it's a string, create a RecoveredError
                    if let Some(s) = any.downcast_ref::<String>() {
                        let error = RecoveredError {
                            error: Box::new(anyhow!("{}", s)),
                        };
                        return Err(Box::new(error));
                    } else if let Some(s) = any.downcast_ref::<&'static str>() {
                        let error = RecoveredError {
                            error: Box::new(anyhow!("{}", s)),
                        };
                        return Err(Box::new(error));
                    }
                }
                
                // If we couldn't recover a specific error message, use a generic one
                let error = RecoveredError {
                    error: Box::new(anyhow!("Task panicked with unknown payload")),
                };
                Err(Box::new(error))
            } else {
                Err(Box::new(Error::from(format!("Task failed: {}", e))))
            }
        }
    }
}

/// Handle encryption requests
async fn handle_encrypt(
    ctx: &lambda_runtime::Context,
    event: MyEvent,
    metrics_provider: Arc<LambdaMetricsProvider>,
) -> Result<MyResponse, Error> {
    tracing::info!("Handling encrypt for {}", event.name);
    print_metrics("encrypt.getsession");
    
    let factory = init_factory().await?;
    let session = factory.session(&event.partition).await
        .map_err(|e| Error::from(format!("Failed to create session: {}", e)))?;
    
    // Use a separate block to ensure session.close() is called at the end
    let result = {
        let payload = event.payload.ok_or_else(|| Error::from("Payload missing"))?;
        
        print_metrics("encrypt.encrypt");
        let enc_data = session.encrypt(&payload).await
            .map_err(|e| Error::from(format!("Encryption failed: {}", e)))?;
        
        MyResponse {
            plain_text: None,
            drr: Some(enc_data),
            metrics: metrics_provider.get_metrics(),
        }
    };
    
    print_metrics("encrypt.close");
    session.close().await
        .map_err(|e| Error::from(format!("Failed to close session: {}", e)))?;
    
    Ok(result)
}

/// Handle decryption requests
async fn handle_decrypt(
    ctx: &lambda_runtime::Context,
    event: MyEvent,
    metrics_provider: Arc<LambdaMetricsProvider>,
) -> Result<MyResponse, Error> {
    tracing::info!("Handling decrypt for {}", event.name);
    print_metrics("decrypt.getsession");
    
    let factory = init_factory().await?;
    let session = factory.session(&event.partition).await
        .map_err(|e| Error::from(format!("Failed to create session: {}", e)))?;
    
    // Use a separate block to ensure session.close() is called at the end
    let result = {
        let drr = event.drr.ok_or_else(|| Error::from("DRR missing"))?;
        
        print_metrics("decrypt.decrypt");
        let plaintext = session.decrypt(&drr).await
            .map_err(|e| Error::from(format!("Decryption failed: {}", e)))?;
        
        MyResponse {
            plain_text: Some(String::from_utf8_lossy(&plaintext).to_string()),
            drr: None,
            metrics: metrics_provider.get_metrics(),
        }
    };
    
    print_metrics("decrypt.close");
    session.close().await
        .map_err(|e| Error::from(format!("Failed to close session: {}", e)))?;
    
    Ok(result)
}

/// Main Lambda function handler
async fn function_handler(event: LambdaEvent<MyEvent>) -> Result<MyResponse, Error> {
    let (event, ctx) = event.into_parts();
    tracing::info!("Processing event: {}", event.name);
    
    // Increment invocations counter
    INVOCATION_COUNTER.increment(1);
    print_metrics("handlerequest.init");
    
    // Create metrics provider
    let metrics_provider = Arc::new(LambdaMetricsProvider::new());
    set_metrics_provider(metrics_provider.clone());
    
    // Try to handle the event
    match try_handle(&ctx, event, metrics_provider.clone()).await {
        Ok(response) => Ok(response),
        Err(e) => {
            // Check if it's a recoverable error
            if let Some(recovered_error) = e.downcast_ref::<RecoveredError>() {
                if recovered_error.is_retryable() {
                    tracing::info!("Recovered from panic with retryable error. Retrying...");
                    print_metrics("handlerequest.retry");
                    
                    // Reset the factory and try again
                    reset_factory().await?;
                    
                    // Recreate the event from the context
                    let retry_event = MyEvent {
                        name: ctx.invoked_function_arn.clone(),
                        partition: "retry".to_string(),
                        payload: None,
                        drr: None,
                    };
                    
                    // Try again - don't catch errors this time
                    try_handle(&ctx, retry_event, metrics_provider.clone()).await
                        .map_err(|e| Error::from(format!("Retry failed: {}", e)))
                } else {
                    Err(Error::from(format!("Unrecoverable error: {}", e)))
                }
            } else {
                Err(Error::from(format!("Error handling request: {}", e)))
            }
        }
    }
}

#[tokio::main]
async fn main() -> Result<(), Error> {
    // Initialize tracing
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::INFO)
        .with_ansi(false) // AWS Lambda does not support ANSI colors
        .init();
    
    // Start the Lambda function
    let func = service_fn(function_handler);
    lambda_runtime::run(func).await?;
    
    Ok(())
}