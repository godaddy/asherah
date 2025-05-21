use appencryption::{
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use securememory::protected_memory::DefaultSecretFactory;
use std::collections::HashMap;
use std::sync::Arc;

/// This example demonstrates advanced usage of AWS KMS integration with Asherah
/// including regional setup for resilience.
///
/// It shows:
/// 1. Setting up AWS KMS with multiple regions
/// 2. Defining a preferred region for normal operations
/// 3. Configuring the session factory with AWS KMS
/// 4. Basic encrypt/decrypt operations

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("AWS KMS Multi-Region Example");
    println!("===========================");

    // Set up AWS KMS with multiple regions
    // Region Map: key = region name, value = KMS key ARN in that region
    let mut arn_map = HashMap::new();
    arn_map.insert(
        "us-west-2".to_string(),
        "arn:aws:kms:us-west-2:123456789012:key/11111111-1111-1111-1111-111111111111".to_string(),
    );
    arn_map.insert(
        "us-east-1".to_string(),
        "arn:aws:kms:us-east-1:123456789012:key/22222222-2222-2222-2222-222222222222".to_string(),
    );
    arn_map.insert(
        "eu-west-1".to_string(),
        "arn:aws:kms:eu-west-1:123456789012:key/33333333-3333-3333-3333-333333333333".to_string(),
    );

    println!("Configuring AWS KMS with multi-region support...");
    println!("  Primary region: us-west-2");
    println!("  Additional regions: us-east-1, eu-west-1");

    // Configure AWS KMS
    #[cfg(feature = "aws-v2-kms")]
    let kms = {
        use appencryption::crypto::Aes256GcmAead;
        use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;

        let crypto = Arc::new(Aes256GcmAead::default());

        let builder = AwsKmsBuilder::new(crypto, arn_map).with_preferred_region("us-west-2");

        Arc::new(builder.build().await?)
    };

    #[cfg(not(feature = "aws-v2-kms"))]
    let kms = {
        // Fall back to static KMS if AWS KMS feature is not enabled
        use appencryption::kms::StaticKeyManagementService;
        let master_key = vec![0_u8; 32];
        Arc::new(StaticKeyManagementService::new(master_key))
    };

    println!("KMS configured successfully!");

    // Create other dependencies
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24);
    let cache_max_age = TimeDelta::hours(2);
    let create_date_precision = TimeDelta::minutes(1);

    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap());

    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory with our KMS implementation
    let factory = Arc::new(SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
        vec![],
    ));

    println!("Creating session and encrypting data...");

    // Create a session and encrypt data
    let session = factory.session("user123").await?;
    let data =
        b"This is sensitive data that will be encrypted using AWS KMS for envelope encryption"
            .to_vec();

    // Encrypt data (this will use the KMS client)
    let encrypted = session.encrypt(&data).await?;
    println!(
        "Successfully encrypted data: {} bytes",
        encrypted.data.len()
    );
    println!("Encrypted with key: {}", encrypted.key.id);

    // Decrypt the data
    let decrypted = session.decrypt(&encrypted).await?;
    println!(
        "Successfully decrypted data: {}",
        String::from_utf8_lossy(&decrypted)
    );

    // Create sessions in different regions (simulated by partition names)
    println!("\nSimulating regional operations:");

    let west_session = factory.session("user-west").await?;
    let east_session = factory.session("user-east").await?;

    // Encrypt data in west region
    let west_data = b"Data encrypted in west region".to_vec();
    let west_encrypted = west_session.encrypt(&west_data).await?;
    println!("Encrypted data in west region");

    // Decrypt in east region (cross-region decrypt)
    let cross_region_decrypted = east_session.decrypt(&west_encrypted).await?;
    println!(
        "Successfully decrypted west data in east region: {}",
        String::from_utf8_lossy(&cross_region_decrypted)
    );

    // Close the sessions when done
    println!("\nClosing sessions...");
    session.close().await?;
    west_session.close().await?;
    east_session.close().await?;

    println!("\nAll operations completed successfully!");

    Ok(())
}
