// Tests to verify concurrent access to sessions

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID,
};
use appencryption::{metastore::InMemoryMetastore, Session, SessionFactory};
use futures::future::join_all;
use rand::{thread_rng, Rng};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use tokio::task;

const NUM_THREADS: usize = 10;
const OPERATIONS_PER_THREAD: usize = 50;

#[tokio::test]
async fn test_concurrent_encrypt_decrypt_with_same_session() {
    if option_env!("SKIP_SLOW_TESTS").is_some() {
        return;
    }

    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory
    let policy = (*config.policy).clone();
    let factory = SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session
    let session = Arc::new(
        factory
            .session(PARTITION_ID)
            .await
            .expect("Failed to get session"),
    );

    // Create multiple tasks that will use the session concurrently
    let mut tasks = Vec::new();

    for i in 0..NUM_THREADS {
        let session_clone = session.clone();

        let task = task::spawn(async move {
            let mut results = Vec::new();

            for j in 0..OPERATIONS_PER_THREAD {
                // Create unique data for this thread/operation
                let data = format!("{}_{}_data_{}", ORIGINAL_DATA, i, j);
                let data_bytes = data.as_bytes().to_vec();

                // Encrypt the data
                let drr = session_clone
                    .encrypt(&data_bytes)
                    .await
                    .expect("Failed to encrypt data");

                // Decrypt the data
                let decrypted = session_clone
                    .decrypt(&drr)
                    .await
                    .expect("Failed to decrypt data");

                // Verify the decryption
                assert_eq!(data_bytes, decrypted);

                results.push((data, drr));
            }

            results
        });

        tasks.push(task);
    }

    // Wait for all tasks to complete
    let all_results = join_all(tasks).await;

    // Verify each task succeeded
    for result in all_results {
        assert!(result.is_ok(), "Task failed");
    }
}

#[tokio::test]
async fn test_concurrent_encrypt_decrypt_with_multiple_sessions() {
    if option_env!("SKIP_SLOW_TESTS").is_some() {
        return;
    }

    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory
    let policy = (*config.policy).clone();
    let factory = Arc::new(SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    ));

    // Create multiple tasks, each creating its own session
    let mut tasks = Vec::new();

    for i in 0..NUM_THREADS {
        let factory_clone = factory.clone();
        let partition_id = format!("{}_{}", PARTITION_ID, i);

        let task = task::spawn(async move {
            // Create a session for this thread
            let session = factory_clone
                .session(&partition_id)
                .await
                .expect("Failed to get session");

            let mut results = Vec::new();

            for j in 0..OPERATIONS_PER_THREAD {
                // Create unique data for this thread/operation
                let data = format!("{}_{}_data_{}", ORIGINAL_DATA, i, j);
                let data_bytes = data.as_bytes().to_vec();

                // Encrypt the data
                let drr = session
                    .encrypt(&data_bytes)
                    .await
                    .expect("Failed to encrypt data");

                // Decrypt the data
                let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

                // Verify the decryption
                assert_eq!(data_bytes, decrypted);

                results.push((data, drr));
            }

            results
        });

        tasks.push(task);
    }

    // Wait for all tasks to complete
    let all_results = join_all(tasks).await;

    // Verify each task succeeded
    for result in all_results {
        assert!(result.is_ok(), "Task failed");
    }
}

#[tokio::test]
async fn test_concurrent_factory_operations() {
    if option_env!("SKIP_SLOW_TESTS").is_some() {
        return;
    }

    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory
    let policy = (*config.policy).clone();
    let factory = Arc::new(SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    ));

    // Create multiple tasks that will use the factory concurrently
    let mut tasks = Vec::new();

    for i in 0..NUM_THREADS {
        let factory_clone = factory.clone();

        let task = task::spawn(async move {
            for _ in 0..OPERATIONS_PER_THREAD {
                // Select a random partition from a small set
                // to increase likelihood of cache hits/contention
                let partition_index = {
                    let mut rng = thread_rng();
                    rng.gen_range(0..5)
                };
                let partition_id = format!("{}_{}", PARTITION_ID, partition_index);

                // Get a session for this partition
                let session = factory_clone
                    .session(&partition_id)
                    .await
                    .expect("Failed to get session");

                // Create data for this operation
                let data = format!("{}_data_{}", ORIGINAL_DATA, i);
                let data_bytes = data.as_bytes().to_vec();

                // Encrypt the data
                let drr = session
                    .encrypt(&data_bytes)
                    .await
                    .expect("Failed to encrypt data");

                // Decrypt the data
                let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

                // Verify the decryption
                assert_eq!(data_bytes, decrypted);
            }
        });

        tasks.push(task);
    }

    // Wait for all tasks to complete
    let all_results = join_all(tasks).await;

    // Verify each task succeeded
    for result in all_results {
        assert!(result.is_ok(), "Task failed");
    }
}
