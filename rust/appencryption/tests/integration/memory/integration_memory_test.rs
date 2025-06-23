// Memory-based integration tests for AppEncryption
// Similar to the Go implementation's integration_memory_test.go

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID,
};
use appencryption::{metastore::InMemoryMetastore, Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::test]
async fn test_session_factory_with_memory_metastore_encrypt_decrypt() {
    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory
    let policy = (*config.policy).clone();
    let factory = SessionFactory::new(
        config.service.clone(),
        config.product.clone(),
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Encrypt data
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let drr = session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Check the parent key meta ID
    assert_eq!(
        format!("_IK_{}_{}_{}", PARTITION_ID, config.service, config.product),
        drr.key.parent_key_meta.as_ref().unwrap().id
    );

    // Decrypt data
    let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

    // Verify decryption
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());
}

#[tokio::test]
async fn test_session_factory_with_memory_metastore_decrypt_with_mismatch_partition_should_fail() {
    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;
    let metastore = Arc::new(InMemoryMetastore::new());

    // Create session factory
    let policy = (*config.policy).clone();
    let factory = SessionFactory::new(
        config.service.clone(),
        config.product.clone(),
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session for original partition
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Encrypt data
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let drr = session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Decrypt with the same session (should work)
    let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

    // Create new session with different partition
    let alt_partition = format!("{}alt", PARTITION_ID);
    let alt_session = factory
        .session(&alt_partition)
        .await
        .expect("Failed to get session with alternate partition");

    // Try to decrypt with the new session (should fail)
    let decrypt_result = alt_session.decrypt(&drr).await;
    assert!(
        decrypt_result.is_err(),
        "Decrypt should fail with mismatched partition"
    );
}
