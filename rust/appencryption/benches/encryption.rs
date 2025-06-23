#![allow(clippy::unseparated_literal_suffix)]

use appencryption::{
    crypto::aes256gcm::Aes256GcmAead,
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
    Aead,
};
use criterion::{criterion_group, criterion_main, BenchmarkId, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::time::Duration;

async fn encrypt_decrypt<T: Session + Sync>(
    session: &T,
    data_size: usize,
) -> Result<(), Box<dyn std::error::Error>> {
    // Generate random data of the specified size
    let data = vec![1u8; data_size];

    // Encrypt the data
    let encrypted = session.encrypt(&data).await?;

    // Decrypt the data
    let decrypted = session.decrypt(&encrypted).await?;

    // Ensure the decrypted data matches the original
    assert_eq!(data, decrypted);

    Ok(())
}


fn encrypt_decrypt_benchmark(c: &mut Criterion) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    let factory = rt.block_on(async {
        // Create a policy with reasonable defaults
        let policy = CryptoPolicy::new();

        // Create a static KMS with a test key
        let master_key = vec![0_u8; 32];
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

        factory
    });

    let session = rt.block_on(async { factory.session("benchmark_partition").await.unwrap() });

    let mut group = c.benchmark_group("encrypt_decrypt");

    // Benchmark different data sizes
    for size in [100, 1_000, 10_000, 100_000].iter() {
        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, &size| {
            b.to_async(&rt)
                .iter(|| encrypt_decrypt(session.as_ref(), size));
        });
    }

    group.finish();
}

fn raw_encryption_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("raw_encryption");
    let aead = Aes256GcmAead::new();

    // Generate a random key
    let key = vec![0_u8; 32];

    // Benchmark different data sizes
    for size in [100, 1_000, 10_000, 100_000].iter() {
        let data = vec![1_u8; *size];

        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, _| {
            b.iter(|| {
                let ciphertext = aead.encrypt(&key, &data).unwrap();
                let plaintext = aead.decrypt(&key, &ciphertext).unwrap();
                assert_eq!(data, plaintext);
            });
        });
    }

    group.finish();
}

criterion_group! {
    name = benches;
    config = Criterion::default()
        .sample_size(50)
        .measurement_time(Duration::from_secs(5))
        .warm_up_time(Duration::from_secs(1));
    targets = encrypt_decrypt_benchmark, raw_encryption_benchmark
}

criterion_main!(benches);
