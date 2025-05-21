use appencryption::{
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::SessionFactory,
};
use criterion::{criterion_group, criterion_main, BatchSize, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::time::Duration;

async fn create_session(factory: &Arc<SessionFactory>, partition_id: &str) -> Result<(), Box<dyn std::error::Error>> {
    let session = factory.session(partition_id).await?;
    
    // We don't need to close the session in this benchmark
    // since we're testing session creation performance
    
    Ok(())
}

async fn rotate_keys(factory: &Arc<SessionFactory>) -> Result<(), Box<dyn std::error::Error>> {
    // Force a system key rotation
    let _ = factory.rotate_system_keys().await?;
    
    Ok(())
}

fn key_rotation_benchmark(c: &mut Criterion) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    
    let factory = rt.block_on(async {
        // Create a policy with short expiration to test key rotation
        let policy = CryptoPolicy::new()
            .with_key_rotation(std::time::Duration::from_secs(3600)); // Short rotation period
        
        // Create a static KMS with a test key
        let master_key = vec![0u8; 32];
        let kms = Arc::new(StaticKeyManagementService::new(master_key));
        
        // Create an in-memory metastore
        let metastore = Arc::new(InMemoryMetastore::new());
        
        // Create a secret factory
        let secret_factory = Arc::new(DefaultSecretFactory::new());
        
        // Create the session factory
        let factory = Arc::new(SessionFactory::new(
            "benchmark",
            "service",
            policy,
            kms,
            metastore,
            secret_factory,
            vec![],
        ));
        
        // Create an initial session to set up the key hierarchy
        let session = factory.session("setup").await.unwrap();
        let test_data = b"test data".to_vec();
        let _ = session.encrypt(&test_data).await.unwrap();
        
        factory
    });
    
    c.bench_function("key_rotation", |b| {
        b.to_async(&rt).iter(|| rotate_keys(&factory));
    });
}

fn session_creation_benchmark(c: &mut Criterion) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    
    let factory = rt.block_on(async {
        // Create a policy with reasonable defaults
        let policy = CryptoPolicy::new();
        
        // Create a static KMS with a test key
        let master_key = vec![0u8; 32];
        let kms = Arc::new(StaticKeyManagementService::new(master_key));
        
        // Create an in-memory metastore
        let metastore = Arc::new(InMemoryMetastore::new());
        
        // Create a secret factory
        let secret_factory = Arc::new(DefaultSecretFactory::new());
        
        // Create the session factory
        let factory = Arc::new(SessionFactory::new(
            "benchmark",
            "service",
            policy,
            kms,
            metastore,
            secret_factory,
            vec![],
        ));
        
        // Create an initial session to set up the key hierarchy
        let session = factory.session("setup").await.unwrap();
        let test_data = b"test data".to_vec();
        let _ = session.encrypt(&test_data).await.unwrap();
        
        factory
    });
    
    let mut counter = 0;
    
    c.bench_function("session_creation", |b| {
        b.to_async(&rt).iter_batched(
            || {
                counter += 1;
                format!("partition_{}", counter)
            },
            |partition_id| create_session(&factory, &partition_id),
            BatchSize::SmallInput,
        );
    });
}

criterion_group! {
    name = benches;
    config = Criterion::default()
        .sample_size(50)
        .measurement_time(Duration::from_secs(5))
        .warm_up_time(Duration::from_secs(1));
    targets = session_creation_benchmark, key_rotation_benchmark
}

criterion_main!(benches);