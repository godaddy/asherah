// DynamoDB integration tests for AppEncryption
// Similar to the Go implementation's integration_dynamodb_test.go

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID,
};
use crate::integration::dynamodb::dynamodb_test_context::DynamoDbTestContext;
use appencryption::{Session, SessionFactory};
use chrono::Utc;
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::test]
async fn test_session_factory_with_dynamodb_metastore_encrypt_decrypt() {
    if option_env!("SKIP_SLOW_TESTS").is_some() {
        return;
    }

    // Create test context
    let created = Utc::now().timestamp() - 24 * 60 * 60; // 24 hours ago
    let test_context = DynamoDbTestContext::new(Some(created));

    // Don't seed the database - let the system create its own keys
    // test_context.seed_db().await;

    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;

    // Get metastore with suffix disabled
    let metastore = test_context.new_metastore(false);

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

    // Test encryption/decryption in a block to ensure session is dropped
    let data_row_record = {
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

        // Decrypt with the same session
        let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

        // Verify decryption
        assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

        drr
    };

    // Now create a new session with the same factory
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Decrypt with the new session
    let decrypted = session
        .decrypt(&data_row_record)
        .await
        .expect("Failed to decrypt data");

    // Verify decryption still works
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

    // Clean up
    test_context.clean_db().await;
}

#[tokio::test]
async fn test_session_factory_with_dynamodb_metastore_encrypt_decrypt_region_suffix_backwards_compatibility(
) {
    if option_env!("SKIP_SLOW_TESTS").is_some() {
        return;
    }

    // Create test context
    let created = Utc::now().timestamp() - 24 * 60 * 60; // 24 hours ago
    let test_context = DynamoDbTestContext::new(Some(created));

    // Don't seed the database - let the system create its own keys
    // test_context.seed_db().await;

    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;

    // First, encrypt using a default, non-suffixed DynamoDB metastore
    let data_row_record = {
        // Get metastore with suffix disabled
        let metastore = test_context.new_metastore(false);

        // Create session factory
        let policy = (*config.policy).clone();
        let factory = SessionFactory::new(
            config.service.clone(),
            config.product.clone(),
            policy,
            kms.clone(),
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

        // Decrypt with the same session
        let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

        // Verify decryption
        assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

        drr
    };

    // Now decrypt using a new factory with a suffixed DynamoDB metastore

    // Get metastore with suffix enabled
    let metastore = test_context.new_metastore(true);

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
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Decrypt with the new session
    let decrypted = session
        .decrypt(&data_row_record)
        .await
        .expect("Failed to decrypt data");

    // Verify decryption still works
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

    // Clean up
    test_context.clean_db().await;
}
