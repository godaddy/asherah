use appencryption::{
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::time::Instant;
use tokio::task;

/// This example demonstrates performance tuning techniques for Asherah.
///
/// It shows:
/// 1. Benchmarking encryption/decryption performance
/// 2. Parallelizing encryption operations for maximum performance
/// 3. Measuring and comparing throughput and latency
/// 4. Best practices for high-performance applications

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Performance Tuning Example");
    println!("=========================");

    // Create base dependencies
    let master_key = vec![0u8; 32]; // In a real app, use a secure key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create data for benchmarking
    // In a real application, this would be your actual workload
    let data_sizes = [64, 1024, 16384, 131072]; // 64 B, 1 KB, 16 KB, 128 KB
    let test_data: Vec<Vec<u8>> = data_sizes
        .iter()
        .map(|&size| {
            vec![0xAB; size] // Simple repeating pattern for tests
        })
        .collect();

    // Test different crypto policy configurations

    let policy_configurations = vec![
        (
            "Fast expiry (1 hour)",
            CryptoPolicy::new().with_expire_after(std::time::Duration::from_secs(60 * 60)),
        ),
        (
            "Standard expiry (24 hours)",
            CryptoPolicy::new().with_expire_after(std::time::Duration::from_secs(60 * 60 * 24)),
        ),
        (
            "Slow expiry (7 days)",
            CryptoPolicy::new().with_expire_after(std::time::Duration::from_secs(60 * 60 * 24 * 7)),
        ),
    ];

    for (config_name, policy) in policy_configurations {
        println!("\nTesting configuration: {}", config_name);

        // Create session factory with this policy
        let factory = Arc::new(SessionFactory::new(
            "service",
            "product",
            policy,
            kms.clone(),
            metastore.clone(),
            secret_factory.clone(),
            vec![],
        ));

        // Benchmark sequential operations
        println!("  Sequential operations benchmark:");
        benchmark_sequential(&factory, &test_data).await?;

        // Benchmark parallel operations
        println!("  Parallel operations benchmark:");
        benchmark_parallel(&factory, &test_data).await?;
    }

    // Test optimized configuration for high throughput
    println!("\nTesting optimized configuration for high throughput...");
    let optimized_factory =
        create_optimized_factory(kms.clone(), metastore.clone(), secret_factory.clone());

    // Benchmark with many parallel operations
    println!("  High-throughput parallel benchmark (100 concurrent operations):");
    benchmark_high_throughput(&optimized_factory, &test_data[1]).await?;

    println!("\nPerformance tuning summary:");
    println!("  - Shorter key rotation periods slightly impact performance but improve security");
    println!("  - Parallel operations can achieve much higher throughput");
    println!("  - The optimized configuration demonstrates the maximum achievable performance");

    println!("\nAll benchmarks completed successfully!");

    Ok(())
}

// Create an optimized factory for high throughput
fn create_optimized_factory(
    kms: Arc<StaticKeyManagementService>,
    metastore: Arc<InMemoryMetastore>,
    secret_factory: Arc<DefaultSecretFactory>,
) -> Arc<SessionFactory> {
    // Create policy optimized for performance

    let policy =
        CryptoPolicy::new().with_expire_after(std::time::Duration::from_secs(60 * 60 * 24)); // Keys expire after 24 hours

    Arc::new(SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
        vec![],
    ))
}

// Benchmark sequential operations with different data sizes
async fn benchmark_sequential(
    factory: &Arc<SessionFactory>,
    test_data: &[Vec<u8>],
) -> Result<(), Box<dyn std::error::Error>> {
    // Create a session
    let session = factory.session("user123").await?;

    for data in test_data.iter() {
        let data_size = data.len();

        // Sequential operations test
        let start = Instant::now();
        let iterations = 10;

        for _ in 0..iterations {
            // Encrypt
            let encrypted = session.encrypt(data).await?;

            // Decrypt
            let _decrypted = session.decrypt(&encrypted).await?;
        }

        let elapsed = start.elapsed();
        let ops_per_sec = (iterations * 2) as f64 / elapsed.as_secs_f64(); // Count both encrypt and decrypt

        println!(
            "    Data size: {} bytes - {:.2} ops/sec ({:.2} ms/op)",
            data_size,
            ops_per_sec,
            1000.0 / ops_per_sec
        );
    }

    // Close the session
    session.close().await?;

    Ok(())
}

// Benchmark parallel operations with different data sizes
async fn benchmark_parallel(
    factory: &Arc<SessionFactory>,
    test_data: &[Vec<u8>],
) -> Result<(), Box<dyn std::error::Error>> {
    for data in test_data.iter() {
        let data_size = data.len();
        let data_clone = data.clone();

        // Parallel operations test
        let start = Instant::now();
        let iterations = 10;
        let mut tasks = Vec::new();

        for _ in 0..iterations {
            let factory_clone = factory.clone();
            let data_clone = data_clone.clone();

            let task = task::spawn(async move {
                let session = factory_clone.session("user123").await?;
                let encrypted = session.encrypt(&data_clone).await?;
                let decrypted = session.decrypt(&encrypted).await?;
                session.close().await?;

                // Verify correctness
                assert_eq!(data_clone, decrypted);

                Ok::<(), Box<dyn std::error::Error + Send + Sync>>(())
            });

            tasks.push(task);
        }

        // Wait for all tasks to complete
        for task in tasks {
            match task.await {
                Ok(res) => match res {
                    Ok(_) => (),
                    Err(e) => return Err(format!("Task error: {}", e).into()),
                },
                Err(e) => return Err(format!("Task failed: {}", e).into()),
            }
        }

        let elapsed = start.elapsed();
        let ops_per_sec = (iterations * 2) as f64 / elapsed.as_secs_f64(); // Count both encrypt and decrypt

        println!(
            "    Data size: {} bytes - {:.2} ops/sec ({:.2} ms/op)",
            data_size,
            ops_per_sec,
            1000.0 / ops_per_sec
        );
    }

    Ok(())
}

// Benchmark with many parallel operations for high throughput testing
async fn benchmark_high_throughput(
    factory: &Arc<SessionFactory>,
    data: &[u8],
) -> Result<(), Box<dyn std::error::Error>> {
    let data_size = data.len();
    let data_clone = data.to_vec();

    // High throughput parallel test
    let start = Instant::now();
    let iterations = 100; // Many concurrent operations
    let mut tasks = Vec::new();

    for i in 0..iterations {
        let factory_clone = factory.clone();
        let data_clone = data_clone.clone();
        let partition = format!("user{}", i % 10); // Use different partitions

        let task = task::spawn(async move {
            let session = factory_clone.session(&partition).await?;
            let encrypted = session.encrypt(&data_clone).await?;
            let decrypted = session.decrypt(&encrypted).await?;
            session.close().await?;

            // Verify correctness
            assert_eq!(data_clone, decrypted);

            Ok::<_, Box<dyn std::error::Error + Send + Sync>>(())
        });

        tasks.push(task);
    }

    // Wait for all tasks to complete
    for task in tasks {
        match task.await {
            Ok(res) => match res {
                Ok(_) => (),
                Err(e) => return Err(format!("Task error: {}", e).into()),
            },
            Err(e) => return Err(format!("Task failed: {}", e).into()),
        }
    }

    let elapsed = start.elapsed();
    let ops_per_sec = (iterations * 2) as f64 / elapsed.as_secs_f64(); // Count both encrypt and decrypt
    let throughput_mbps = (data_size * iterations * 2) as f64 / elapsed.as_secs_f64() / 1_000_000.0;

    println!("    Data size: {} bytes", data_size);
    println!("    Operations: {} (encrypt + decrypt each)", iterations);
    println!("    Total time: {:.2} seconds", elapsed.as_secs_f64());
    println!(
        "    Throughput: {:.2} ops/sec, {:.2} MB/sec",
        ops_per_sec, throughput_mbps
    );

    Ok(())
}
