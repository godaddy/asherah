//! Example demonstrating AWS KMS integration for Asherah
//!
//! This example shows how to use AWS KMS for key management.
//! To run this example:
//! ```bash
//! cargo run --example aws_v1_integration --features aws-v2-kms
//! ```

use appencryption::metastore::InMemoryMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::{Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Set up logging
    env_logger::init();

    println!("AWS KMS Integration Example");
    println!("=========================");

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

    // Create AWS KMS
    #[cfg(feature = "aws-v2-kms")]
    let kms = {
        use appencryption::crypto::Aes256GcmAead;
        use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;

        let crypto = Arc::new(Aes256GcmAead::default());

        // Replace with your actual AWS KMS ARNs
        let mut arn_map = std::collections::HashMap::new();
        arn_map.insert(
            "us-east-1".to_string(),
            "arn:aws:kms:us-east-1:123456789012:key/abcd1234-a123-456a-a12b-a123b4cd56ef"
                .to_string(),
        );

        let builder = AwsKmsBuilder::new(crypto, arn_map).with_preferred_region("us-east-1");

        Arc::new(builder.build().await?)
    };

    #[cfg(not(feature = "aws-v2-kms"))]
    let kms = {
        // Fall back to static KMS if AWS KMS feature is not enabled
        use appencryption::kms::StaticKeyManagementService;
        let master_key = vec![0u8; 32];
        Arc::new(StaticKeyManagementService::new(master_key))
    };

    println!("✓ Created KMS service");

    // Create in-memory metastore (in production, use DynamoDB)
    let metastore = Arc::new(InMemoryMetastore::new());
    println!("✓ Created metastore");

    // Create secret factory
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = Arc::new(SessionFactory::new(
        "test-service",
        "test-product",
        policy,
        kms,
        metastore,
        secret_factory,
        vec![],
    ));
    println!("✓ Created session factory");

    // Create session for encryption/decryption
    let session = factory.session("user123").await?;
    println!("✓ Created session");

    // Example data
    let data = b"This is a secret message";
    println!("\nOriginal data: {:?}", std::str::from_utf8(data)?);

    // Encrypt data
    let encrypted = session.encrypt(data).await?;
    println!("Encrypted data: {} bytes", encrypted.data.len());
    println!("Encrypted with key: {}", encrypted.key.id);

    // Decrypt data
    let decrypted = session.decrypt(&encrypted).await?;
    println!("Decrypted data: {:?}", std::str::from_utf8(&decrypted)?);

    // Verify data is the same
    assert_eq!(data, &decrypted[..]);
    println!("✓ Encryption/decryption successful");

    // Close session
    session.close().await?;
    println!("✓ Session closed");

    Ok(())
}
