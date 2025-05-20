use appencryption::session::{Session, SessionFactory};
use appencryption::envelope::DataRowRecord;
use appencryption::policy::CryptoPolicy;
#[cfg(feature = "mysql")]
use appencryption::metastore::MySqlMetastore;
#[cfg(feature = "postgres")]
use appencryption::metastore::PostgresMetastore;
use appencryption::metastore::InMemoryMetastore;
use appencryption::kms::StaticKeyManagementService;
use appencryption::Metastore;
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::time::Instant;
use tokio::sync::Mutex;
use rand::{Rng, thread_rng};
use rand::distributions::Alphanumeric;
use clap::{Parser, Subcommand, ValueEnum};
use std::collections::HashMap;
use serde::{Serialize, Deserialize};

#[derive(Parser)]
#[command(author, version, about, long_about = None)]
struct Cli {
    /// Number of operations to perform per session
    #[arg(short, long, default_value_t = 1000)]
    count: usize,

    /// Number of iterations to run
    #[arg(short, long, default_value_t = 1)]
    iterations: usize,

    /// Number of concurrent sessions to use
    #[arg(short, long, default_value_t = 20)]
    sessions: usize,

    /// Enable verbose logging
    #[arg(short, long)]
    verbose: bool,

    /// Enable progress output
    #[arg(short = 'P', long)]
    progress: bool,

    /// Print encryption/decryption results
    #[arg(short, long)]
    results: bool,

    /// Enable metrics collection and output
    #[arg(short, long)]
    metrics: bool,

    /// Duration to run the test (e.g. "10s", "1m")
    #[arg(short, long)]
    duration: Option<humantime::Duration>,

    /// Service name for key hierarchy
    #[arg(long, default_value = "exampleService")]
    service: String,

    /// Product name for key hierarchy
    #[arg(long, default_value = "productId")]
    product: String,

    /// Partition ID to use (defaults to random if not specified)
    #[arg(long)]
    partition: Option<String>,

    /// Enable the session cache
    #[arg(long)]
    session_cache: bool,

    /// Session cache size
    #[arg(long)]
    session_cache_size: Option<usize>,

    /// Session cache expiry in seconds
    #[arg(long)]
    session_cache_expiry: Option<u64>,

    /// Disable caching
    #[arg(long)]
    no_cache: bool,

    /// Key expiry time in seconds
    #[arg(long)]
    expire_after: Option<u64>,

    /// Key check interval in seconds
    #[arg(long)]
    check_interval: Option<u64>,

    /// Enable shared intermediate key cache
    #[arg(long)]
    shared_ik_cache: bool,

    /// Intermediate key cache size
    #[arg(long)]
    ik_cache_size: Option<usize>,

    /// System key cache size
    #[arg(long)]
    sk_cache_size: Option<usize>,

    /// Metastore type to use
    #[arg(long, value_enum, default_value_t = MetastoreType::Memory)]
    metastore: MetastoreType,

    /// KMS type to use
    #[arg(long, value_enum, default_value_t = KmsType::Static)]
    kms: KmsType,

    /// AWS region for AWS KMS
    #[arg(long)]
    region: Option<String>,

    /// AWS KMS key ID
    #[arg(long)]
    key_id: Option<String>,

    /// MySQL connection string (mysql://user:pass@host:port/db)
    #[arg(long)]
    mysql_url: Option<String>,

    /// PostgreSQL connection string (postgres://user:pass@host:port/db)
    #[arg(long)]
    postgres_url: Option<String>,

    /// DynamoDB table name
    #[arg(long, default_value = "encryption_key")]
    dynamodb_table: String,

    /// Truncate (delete all keys) before running
    #[arg(long)]
    truncate: bool,

    #[command(subcommand)]
    command: Option<Commands>,
}

#[derive(Subcommand)]
enum Commands {
    /// Generate a new random key and print it
    GenerateKey,

    /// Generate random test data
    GenerateData {
        /// Number of records to generate
        #[arg(short, long, default_value_t = 1)]
        count: usize,
    },
}

#[derive(Copy, Clone, PartialEq, Eq, ValueEnum)]
enum MetastoreType {
    Memory,
    #[cfg(feature = "mysql")]
    Mysql,
    #[cfg(feature = "postgres")]
    Postgres,
    Dynamodb,
}

#[derive(Copy, Clone, PartialEq, Eq, ValueEnum)]
enum KmsType {
    Static,
    Aws,
}

/// Sample contact record to encrypt
#[derive(Serialize, Deserialize, Debug, Clone)]
struct Contact {
    first_name: String,
    last_name: String,
    addresses: Vec<Address>,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
struct Address {
    street: String,
    city: String,
}

/// Simple metrics tracking for CLI
struct SimpleMetrics {
    metrics: Arc<Mutex<HashMap<String, f64>>>,
}

impl SimpleMetrics {
    fn new() -> Self {
        Self {
            metrics: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    async fn display_metrics(&self) {
        let metrics = self.metrics.lock().await;
        println!("\n=== Metrics ===");
        for (key, value) in metrics.iter() {
            println!("{}: {}", key, value);
        }
    }
    
    async fn increment(&self, name: &str, amount: u64) {
        let mut metrics = self.metrics.lock().await;
        *metrics.entry(name.to_string()).or_insert(0.0) += amount as f64;
    }
    
    async fn record_time(&self, name: &str, duration: std::time::Duration) {
        let mut metrics = self.metrics.lock().await;
        let duration_ms = duration.as_secs_f64() * 1000.0;
        metrics.insert(format!("{}_last", name), duration_ms);
        *metrics.entry(format!("{}_count", name)).or_insert(0.0) += 1.0;
        let key = format!("{}_avg", name);
        let curr_avg = metrics.entry(key.clone()).or_insert(duration_ms).clone();
        let count = *metrics.get(&format!("{}_count", name)).unwrap();
        metrics.insert(key, (curr_avg * (count - 1.0) + duration_ms) / count);
    }
}

/// Generate random string of specified length
fn random_string(length: usize) -> String {
    thread_rng()
        .sample_iter(&Alphanumeric)
        .take(length)
        .map(char::from)
        .collect()
}

/// Generate a new Contact with random data
fn generate_contact() -> Contact {
    Contact {
        first_name: random_string(8),
        last_name: random_string(15),
        addresses: vec![
            Address {
                street: random_string(20),
                city: random_string(10),
            },
            Address {
                street: random_string(20),
                city: random_string(10),
            },
        ],
    }
}

/// Encrypt a contact using the session
async fn encrypt_contact<S: Session>(session: &Arc<S>, contact: &Contact) -> Result<DataRowRecord, Box<dyn std::error::Error>> {
    let data = serde_json::to_vec(contact)?;
    let encrypted = session.encrypt(&data).await?;
    Ok(encrypted)
}

/// Decrypt data using the session
async fn decrypt_data<S: Session>(session: &Arc<S>, data: &DataRowRecord) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
    let decrypted = session.decrypt(data).await?;
    Ok(decrypted)
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let cli = Cli::parse();

    // Handle subcommands
    if let Some(command) = cli.command {
        match command {
            Commands::GenerateKey => {
                let mut rng = thread_rng();
                let key: Vec<u8> = (0..32).map(|_| rng.gen()).collect();
                let hex_key = key.iter().map(|b| format!("{:02x}", b)).collect::<String>();
                println!("Generated key (hex): {}", hex_key);
                return Ok(());
            }
            Commands::GenerateData { count } => {
                for _ in 0..count {
                    let contact = generate_contact();
                    println!("{}", serde_json::to_string_pretty(&contact)?);
                }
                return Ok(());
            }
        }
    }

    // Set up metrics if enabled
    let metrics = Arc::new(SimpleMetrics::new());

    // Configure crypto policy
    use chrono::TimeDelta;
    
    let expire_after = cli.expire_after.map(|secs| TimeDelta::seconds(secs as i64)).unwrap_or(TimeDelta::hours(24));
    let cache_max_age = TimeDelta::hours(24); // Default
    let create_date_precision = TimeDelta::minutes(1); // Default
    
    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap());

    // Create the KMS
    println!("Setting up KMS...");
    let kms: Arc<dyn appencryption::KeyManagementService> = match cli.kms {
        KmsType::Static => {
            println!("Using static KMS");
            let key = vec![0u8; 32]; // Static key for testing only
            Arc::new(StaticKeyManagementService::new(key))
        }
        KmsType::Aws => {
            let region = cli.region.ok_or("AWS region is required for AWS KMS")?;
            let key_id = cli.key_id.ok_or("AWS KMS Key ID is required for AWS KMS")?;
            
            println!("Using AWS KMS with region {} and key {}", region, key_id);
            
            #[cfg(feature = "aws-v2-kms")]
            {
                use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
                use appencryption::crypto::Aes256GcmAead;
                let crypto = Arc::new(Aes256GcmAead::default());
                
                let mut arn_map = std::collections::HashMap::new();
                arn_map.insert(region.clone(), key_id.clone());
                
                let builder = AwsKmsBuilder::new(crypto, arn_map)
                    .with_preferred_region(&region);
                
                Arc::new(builder.build().await?)
            }
            #[cfg(not(feature = "aws-v2-kms"))]
            {
                panic!("AWS KMS support requires aws-v2-kms feature");
            }
        }
    };

    // Create the metastore
    println!("Setting up metastore...");
    let metastore: Arc<dyn Metastore + Send + Sync> = match cli.metastore {
        MetastoreType::Memory => {
            println!("Using in-memory metastore");
            Arc::new(InMemoryMetastore::new())
        }
        #[cfg(feature = "mysql")]
        MetastoreType::Mysql => {
            #[cfg(feature = "mysql")]
            {
                let url = cli.mysql_url.ok_or("MySQL URL is required for MySQL metastore")?;
                println!("Using MySQL metastore with URL {}", url);
                
                use sqlx::mysql::MySqlPoolOptions;
                
                let pool = MySqlPoolOptions::new()
                    .max_connections(5)
                    .connect(&url)
                    .await?;
                
                // Create table if it doesn't exist
                sqlx::query(
                    "CREATE TABLE IF NOT EXISTS encryption_key (
                        id VARCHAR(255) NOT NULL,
                        created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        key_record TEXT NOT NULL,
                        PRIMARY KEY (id, created),
                        INDEX (created)
                    )"
                )
                .execute(&pool)
                .await?;
                
                if cli.truncate {
                    println!("Truncating keys table...");
                    sqlx::query("DELETE FROM encryption_key WHERE 1=1")
                        .execute(&pool)
                        .await?;
                }
                
                Arc::new(MySqlMetastore::new(Arc::new(pool)))
            }
            #[cfg(not(feature = "mysql"))]
            {
                panic!("MySQL support requires mysql feature");
            }
        }
        #[cfg(feature = "postgres")]
        MetastoreType::Postgres => {
            #[cfg(feature = "postgres")]
            {
                let url = cli.postgres_url.ok_or("PostgreSQL URL is required for PostgreSQL metastore")?;
                println!("Using PostgreSQL metastore with URL {}", url);
                
                use sqlx::postgres::PgPoolOptions;
                
                let pool = PgPoolOptions::new()
                    .max_connections(5)
                    .connect(&url)
                    .await?;
                
                // Create table if it doesn't exist
                sqlx::query(
                    "CREATE TABLE IF NOT EXISTS encryption_key (
                        id VARCHAR(255) NOT NULL,
                        created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        key_record TEXT NOT NULL,
                        PRIMARY KEY (id, created)
                    )"
                )
                .execute(&pool)
                .await?;
                
                if cli.truncate {
                    println!("Truncating keys table...");
                    sqlx::query("DELETE FROM encryption_key WHERE TRUE")
                        .execute(&pool)
                        .await?;
                }
                
                Arc::new(PostgresMetastore::new(Arc::new(pool)))
            }
            #[cfg(not(feature = "postgres"))]
            {
                panic!("PostgreSQL support requires postgres feature");
            }
        }
        MetastoreType::Dynamodb => {
            println!("Using DynamoDB metastore with table {}", cli.dynamodb_table);
            
            #[cfg(feature = "aws-v2-dynamodb")]
            {
                use appencryption::plugins::aws_v2::metastore::{DynamoDbMetastore, DynamoDbClientBuilder};
                
                let config = aws_config::from_env().load().await;
                let region = config.region().map(|r| r.to_string()).unwrap_or_else(|| "us-east-1".to_string());
                
                let client = DynamoDbClientBuilder::new(&region)
                    .with_config(config)
                    .build()
                    .await?;
                
                let metastore = DynamoDbMetastore::new(
                    Arc::new(client),
                    Some(cli.dynamodb_table.clone()),
                    false // use_region_suffix
                );
                
                if cli.truncate {
                    println!("Warning: Truncate not implemented for DynamoDB metastore");
                }
                
                Arc::new(metastore)
            }
            #[cfg(not(feature = "aws-v2-dynamodb"))]
            {
                panic!("DynamoDB support requires aws-v2-dynamodb feature")
            }
        }
    };

    // Create the secret factory
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create the session factory
    let factory = Arc::new(SessionFactory::new(
        &cli.service,
        &cli.product,
        policy,
        kms,
        metastore,
        secret_factory.clone(),
        vec![],
    ));

    // Run the workload
    let start = Instant::now();
    let encrypt_times = Arc::new(Mutex::new(Vec::new()));
    let decrypt_times = Arc::new(Mutex::new(Vec::new()));
    
    let default_partition_id = "user123".to_string();
    let partition_id = cli.partition.unwrap_or(default_partition_id);

    println!("Running with {} sessions, {} operations per session, {} iterations", 
        cli.sessions, cli.count, cli.iterations);
    
    for iteration in 0..cli.iterations {
        if cli.verbose {
            println!("Starting iteration {}", iteration + 1);
        }
        
        let mut handles = Vec::new();
        
        for session_idx in 0..cli.sessions {
            let factory = factory.clone();
            let encrypt_times = encrypt_times.clone();
            let decrypt_times = decrypt_times.clone();
            let verbose = cli.verbose;
            let results = cli.results;
            let count = cli.count;
            let partition_id = format!("{}-{}", partition_id, session_idx);
            let duration = cli.duration;
            
            let handle = tokio::spawn(async move {
                if verbose {
                    println!("Starting session with partition ID: {}", partition_id);
                }
                
                let session = factory.session(&partition_id).await.unwrap();
                
                let start_time = Instant::now();
                let mut run_count = 0;
                
                loop {
                    let mut encrypted_records = Vec::with_capacity(count);
                    
                    // Encrypt phase
                    for _ in 0..count {
                        let contact = generate_contact();
                        
                        if results {
                            println!("Before encryption: {}", serde_json::to_string(&contact).unwrap());
                        }
                        
                        let encrypt_start = Instant::now();
                        let encrypted = encrypt_contact(&session, &contact).await.unwrap();
                        let encrypt_duration = encrypt_start.elapsed();
                        
                        if results {
                            println!("After encryption: {:?}", encrypted);
                        }
                        
                        encrypted_records.push(encrypted);
                        encrypt_times.lock().await.push(encrypt_duration);
                    }
                    
                    // Decrypt phase
                    for record in &encrypted_records {
                        let decrypt_start = Instant::now();
                        let decrypted = decrypt_data(&session, record).await.unwrap();
                        let decrypt_duration = decrypt_start.elapsed();
                        
                        if results {
                            let contact: Contact = serde_json::from_slice(&decrypted).unwrap();
                            println!("After decryption: {}", serde_json::to_string(&contact).unwrap());
                        }
                        
                        decrypt_times.lock().await.push(decrypt_duration);
                    }
                    
                    run_count += 1;
                    
                    if let Some(duration) = duration {
                        if start_time.elapsed() >= std::time::Duration::from(duration) {
                            break;
                        }
                    } else {
                        break;
                    }
                }
                
                session.close().await.unwrap();
                
                if verbose {
                    println!("Session completed. Ran {} times", run_count);
                }
            });
            
            handles.push(handle);
        }
        
        // Wait for all sessions to complete
        for handle in handles {
            handle.await?;
        }
    }

    let total_duration = start.elapsed();
    
    // Display timing statistics
    let encrypt_times = encrypt_times.lock().await;
    let decrypt_times = decrypt_times.lock().await;
    
    let total_operations = encrypt_times.len() + decrypt_times.len();
    
    println!("\n=== Performance Summary ===");
    println!("Total time: {:?}", total_duration);
    println!("Total operations: {}", total_operations);
    println!("Operations per second: {:.2}", total_operations as f64 / total_duration.as_secs_f64());
    
    // Calculate encryption stats
    if !encrypt_times.is_empty() {
        let encrypt_total: std::time::Duration = encrypt_times.iter().copied().sum();
        let encrypt_avg = encrypt_total / encrypt_times.len() as u32;
        let encrypt_min = encrypt_times.iter().min().unwrap();
        let encrypt_max = encrypt_times.iter().max().unwrap();
        
        println!("\n=== Encryption ===");
        println!("Count: {}", encrypt_times.len());
        println!("Average: {:?}", encrypt_avg);
        println!("Min: {:?}", encrypt_min);
        println!("Max: {:?}", encrypt_max);
    }
    
    // Calculate decryption stats
    if !decrypt_times.is_empty() {
        let decrypt_total: std::time::Duration = decrypt_times.iter().copied().sum();
        let decrypt_avg = decrypt_total / decrypt_times.len() as u32;
        let decrypt_min = decrypt_times.iter().min().unwrap();
        let decrypt_max = decrypt_times.iter().max().unwrap();
        
        println!("\n=== Decryption ===");
        println!("Count: {}", decrypt_times.len());
        println!("Average: {:?}", decrypt_avg);
        println!("Min: {:?}", decrypt_min);
        println!("Max: {:?}", decrypt_max);
    }
    
    // Display metrics if enabled
    if cli.metrics {
        metrics.display_metrics().await;
    }
    
    Ok(())
}