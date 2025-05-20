use appencryption::{
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use tokio::time::{sleep, Duration};

/// This example demonstrates metrics integration with Asherah.
/// Note: The current API doesn't have built-in metrics support,
/// so this example shows how you might implement custom metrics tracking.

/// Simple custom metrics tracking
struct MetricsCollector {
    encrypt_count: std::sync::atomic::AtomicU64,
    decrypt_count: std::sync::atomic::AtomicU64,
    total_encrypt_time: std::sync::atomic::AtomicU64,
    total_decrypt_time: std::sync::atomic::AtomicU64,
}

impl MetricsCollector {
    fn new() -> Self {
        Self {
            encrypt_count: std::sync::atomic::AtomicU64::new(0),
            decrypt_count: std::sync::atomic::AtomicU64::new(0),
            total_encrypt_time: std::sync::atomic::AtomicU64::new(0),
            total_decrypt_time: std::sync::atomic::AtomicU64::new(0),
        }
    }

    fn record_encrypt(&self, duration: Duration) {
        self.encrypt_count
            .fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        self.total_encrypt_time.fetch_add(
            duration.as_millis() as u64,
            std::sync::atomic::Ordering::Relaxed,
        );
    }

    fn record_decrypt(&self, duration: Duration) {
        self.decrypt_count
            .fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        self.total_decrypt_time.fetch_add(
            duration.as_millis() as u64,
            std::sync::atomic::Ordering::Relaxed,
        );
    }

    fn print_metrics(&self) {
        let encrypt_count = self
            .encrypt_count
            .load(std::sync::atomic::Ordering::Relaxed);
        let decrypt_count = self
            .decrypt_count
            .load(std::sync::atomic::Ordering::Relaxed);
        let total_encrypt_time = self
            .total_encrypt_time
            .load(std::sync::atomic::Ordering::Relaxed);
        let total_decrypt_time = self
            .total_decrypt_time
            .load(std::sync::atomic::Ordering::Relaxed);

        println!("\nMetrics Summary:");
        println!("  Encryption operations: {}", encrypt_count);
        println!("  Decryption operations: {}", decrypt_count);
        if encrypt_count > 0 {
            println!(
                "  Average encryption time: {}ms",
                total_encrypt_time / encrypt_count
            );
        }
        if decrypt_count > 0 {
            println!(
                "  Average decryption time: {}ms",
                total_decrypt_time / decrypt_count
            );
        }
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Metrics Integration Example");
    println!("==========================");

    // Create metrics collector
    let metrics = Arc::new(MetricsCollector::new());

    // Create crypto policy
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24);
    let cache_max_age = TimeDelta::hours(2);
    let create_date_precision = TimeDelta::minutes(1);

    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap());

    // Create dependencies
    println!("Creating Asherah dependencies...");
    let master_key = vec![0u8; 32]; // In a real app, use a secure key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = Arc::new(SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
        vec![],
    ));

    // Create session
    println!("Creating session...");
    let session = factory.session("user123").await?;

    // Perform operations that will generate metrics
    println!("\nPerforming operations to generate metrics...");

    // Run a simulation of multiple operations
    for i in 1..=5 {
        println!("\nOperation batch {} of 5:", i);
        perform_encryption_operations(&session, &metrics, 10).await?;

        // Pause between operations to simulate user activity
        sleep(Duration::from_millis(100)).await;
    }

    // Close the session
    println!("\nClosing session...");
    session.close().await?;

    // Print metrics summary
    metrics.print_metrics();

    println!("\nMetrics example completed successfully!");

    Ok(())
}

// Perform encryption and decryption operations to generate metrics
async fn perform_encryption_operations(
    session: &Arc<impl Session>,
    metrics: &Arc<MetricsCollector>,
    count: usize,
) -> Result<(), Box<dyn std::error::Error>> {
    for i in 0..count {
        // Create some data to encrypt
        let data = format!("Sensitive data for operation {}", i).into_bytes();

        // Encrypt the data with timing
        let start = std::time::Instant::now();
        let encrypted = session.encrypt(&data).await?;
        let encrypt_duration = start.elapsed();
        metrics.record_encrypt(encrypt_duration);
        println!(
            "  Encrypted {} bytes in {}ms",
            data.len(),
            encrypt_duration.as_millis()
        );

        // Decrypt the data with timing
        let start = std::time::Instant::now();
        let decrypted = session.decrypt(&encrypted).await?;
        let decrypt_duration = start.elapsed();
        metrics.record_decrypt(decrypt_duration);
        println!(
            "  Decrypted {} bytes in {}ms",
            decrypted.len(),
            decrypt_duration.as_millis()
        );

        // Validate the decryption
        assert_eq!(data, decrypted);
    }

    Ok(())
}
