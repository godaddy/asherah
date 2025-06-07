# Asherah Plugin Architecture

This directory contains plugin implementations for AWS service integrations.

## Structure

The plugin architecture is organized by AWS SDK version and service:

```
plugins/
├── aws_v1/              # AWS SDK v1 plugins
│   ├── kms/             # KMS implementation
│   └── metastore/       # DynamoDB metastore implementation
└── aws_v2/              # AWS SDK v2 plugins
    ├── kms/             # KMS implementation
    └── metastore/       # DynamoDB metastore implementation
```

## Feature Flags

These plugins are controlled by feature flags in `Cargo.toml`:

- **AWS SDK versions**:
  - `aws-v1`: Enables AWS SDK v1 plugins
  - `aws-v2`: Enables AWS SDK v2 plugins (default)

- **AWS services**:
  - `aws-kms`: Enables KMS plugins
  - `aws-dynamodb`: Enables DynamoDB plugins

- **Combined flags**:
  - `aws-v2-kms`: Enables AWS SDK v2 KMS plugin
  - `aws-v2-dynamodb`: Enables AWS SDK v2 DynamoDB plugin

## Usage

Include only the plugins you need by specifying the appropriate feature flags:

```toml
[dependencies]
appencryption = { version = "0.1.0", features = ["aws-v2-kms", "aws-v2-dynamodb"] }
```

### KMS Example

```rust
use appencryption::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
use appencryption::crypto::aes256gcm::Aes256Gcm;
use std::collections::HashMap;
use std::sync::Arc;

async fn example() -> Result<(), Box<dyn std::error::Error>> {
    // Create AWS KMS client (normally you'd use the AWS SDK)
    let client = create_aws_kms_client("us-west-2").await?;

    // Create ARN map
    let mut arn_map = HashMap::new();
    arn_map.insert(
        "us-west-2".to_string(),
        "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
    );

    // Create crypto implementation
    let crypto = Arc::new(Aes256Gcm::new());

    // Build KMS service
    let kms = AwsKmsBuilder::new(crypto, arn_map)
        .with_kms_client("us-west-2", Arc::new(client))
        .build()?;

    // Use KMS for key operations
    let key_data = vec![1, 2, 3, 4, 5];
    let encrypted = kms.encrypt_key(&key_data).await?;
    let decrypted = kms.decrypt_key(&encrypted).await?;

    assert_eq!(key_data, decrypted);

    Ok(())
}
```

### DynamoDB Metastore Example

```rust
use appencryption::plugins::aws_v2::metastore::{DynamoDbMetastore, DynamoDbClient};
use std::sync::Arc;

async fn example() -> Result<(), Box<dyn std::error::Error>> {
    // Create AWS DynamoDB client (normally you'd use the AWS SDK)
    let primary_client = create_dynamodb_client("us-west-2").await?;
    let replica_client = create_dynamodb_client("us-east-1").await?;

    // Create DynamoDB metastore
    let metastore = DynamoDbMetastore::with_replicas(
        Arc::new(primary_client),
        vec![Arc::new(replica_client)],
        Some("EncryptionKeys".to_string()), // table name
        true, // use region suffix
        false, // don't prefer region
    );

    // Use metastore
    let key_record = create_key_record();
    let stored = metastore.store("my-key", 1234567890, &key_record).await?;
    let loaded = metastore.load_latest("my-key").await?;

    Ok(())
}
```

## Testing

Each plugin includes unit tests that use mock implementations of the AWS services.
These tests verify the behavior and integration with the Asherah encryption system.