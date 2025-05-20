// Tests for cross-partition decryption behavior

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID,
};
use appencryption::{metastore::InMemoryMetastore, Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::test]
async fn test_cross_partition_decrypt_with_different_partitions() {
    // Create dependencies
    let config = create_test_config();
    let crypto = create_crypto();
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

    // Create two sessions with different partition IDs
    let partition1 = PARTITION_ID.to_string();
    let partition2 = format!("{}2", PARTITION_ID);

    let session1 = factory
        .session(&partition1)
        .await
        .expect("Failed to get session 1");

    let session2 = factory
        .session(&partition2)
        .await
        .expect("Failed to get session 2");

    // Encrypt data with session 1
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let drr1 = session1
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data with session 1");

    // Try to decrypt with session 2 (should fail)
    let decrypt_result = session2.decrypt(&drr1).await;
    assert!(
        decrypt_result.is_err(),
        "Cross-partition decrypt should fail"
    );

    // Encrypt data with session 2
    let drr2 = session2
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data with session 2");

    // Try to decrypt session 2's data with session 1 (should fail)
    let decrypt_result = session1.decrypt(&drr2).await;
    assert!(
        decrypt_result.is_err(),
        "Cross-partition decrypt should fail"
    );

    // Verify that each session can decrypt its own data
    let decrypted1 = session1
        .decrypt(&drr1)
        .await
        .expect("Session 1 should decrypt its own data");
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted1.as_slice());

    let decrypted2 = session2
        .decrypt(&drr2)
        .await
        .expect("Session 2 should decrypt its own data");
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted2.as_slice());
}

#[tokio::test]
async fn test_cross_partition_decrypt_with_suffixed_partition() {
    // Create dependencies
    let config = create_test_config();
    let crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory - region suffixing would need to be handled differently
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

    // Create two sessions with different partition IDs to simulate the region suffix behavior
    let partition1 = format!("{}_region1", PARTITION_ID);
    let partition2 = format!("{}_region2", PARTITION_ID);

    let session1 = factory
        .session(&partition1)
        .await
        .expect("Failed to get session 1");

    let session2 = factory
        .session(&partition2)
        .await
        .expect("Failed to get session 2");

    // Encrypt data with session 1
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let drr1 = session1
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data with session 1");

    // Try to decrypt with session 2 (should fail because different region suffixes)
    let decrypt_result = session2.decrypt(&drr1).await;
    assert!(decrypt_result.is_err(), "Cross-region decrypt should fail");

    // Encrypt data with session 2
    let drr2 = session2
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data with session 2");

    // Try to decrypt session 2's data with session 1 (should fail)
    let decrypt_result = session1.decrypt(&drr2).await;
    assert!(decrypt_result.is_err(), "Cross-region decrypt should fail");
}
