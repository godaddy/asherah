use appencryption::policy::CryptoPolicy;
use appencryption::session::{Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    #[cfg(feature = "aws-v2-dynamodb")]
    {
        // Create AWS config for multi-region setup
        use appencryption::plugins::aws_v2::metastore::{DynamoDbClientBuilder, DynamoDbMetastore};

        let config = aws_config::from_env().load().await;
        let region = config
            .region()
            .map(|r| r.to_string())
            .unwrap_or_else(|| "us-west-2".to_string());

        let client = DynamoDbClientBuilder::new(&region)
            .with_config(config)
            .build()
            .await?;

        // Create a DynamoDB metastore
        let metastore = Arc::new(DynamoDbMetastore::new(
            Arc::new(client),
            Some("EncryptionKeys".to_string()),
            true, // Use region suffix to prevent write conflicts
        ));

        // Create KMS
        use appencryption::crypto::Aes256GcmAead;
        use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;

        let crypto = Arc::new(Aes256GcmAead::default());

        // Add KMS keys for multiple regions
        let mut arn_map = std::collections::HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd1234-a123-456a-a12b-a123b4cd56ef"
                .to_string(),
        );
        arn_map.insert(
            "us-east-1".to_string(),
            "arn:aws:kms:us-east-1:123456789012:key/abcd1234-a123-456a-a12b-a123b4cd56ef"
                .to_string(),
        );

        let builder = AwsKmsBuilder::new(crypto, arn_map).with_preferred_region("us-west-2");

        let kms = Arc::new(builder.build().await?);

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

        // Create a secret factory for secure memory
        let secret_factory = Arc::new(DefaultSecretFactory::new());

        // Create a session factory for encryption/decryption
        let factory = Arc::new(SessionFactory::new(
            "my-service",
            "my-product",
            policy,
            kms,
            metastore,
            secret_factory,
            vec![],
        ));

        // Create a session for a specific partition
        let session = factory.session("user123").await?;

        // Encrypt some data
        let data = b"this is my secret data".to_vec();
        let encrypted = session.encrypt(&data).await?;
        println!("Encrypted data length: {}", encrypted.data.len());
        println!("Encrypted with key: {}", encrypted.key.id);

        // Decrypt the data
        let decrypted = session.decrypt(&encrypted).await?;
        println!("Decrypted data: {}", String::from_utf8_lossy(&decrypted));

        // Clean up
        session.close().await?;
    }

    #[cfg(not(feature = "aws-v2-dynamodb"))]
    {
        println!("This example requires the aws-v2-dynamodb feature");
        println!("Run with: cargo run --example dynamodb_global --features aws-v2-dynamodb");
    }

    Ok(())
}
