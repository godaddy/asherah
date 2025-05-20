# AWS SDK v1 Integration for Asherah

This module provides AWS SDK v1 integration for Asherah's application encryption library. It supports:

- AWS KMS for key management service
- DynamoDB for the metastore

## Requirements

To use these plugins, enable the appropriate feature flags:

- `aws-v1-kms` - Enables the AWS KMS integration
- `aws-v1-dynamodb` - Enables the DynamoDB metastore integration
- `aws-v1` - Base AWS v1 integration (enabled by the above features)

## KMS Integration

The KMS integration allows you to use AWS KMS as the master key service. It supports:

- Multiple regions for high availability
- Region preference for performance optimization
- Secure key generation and encryption

### Usage

```rust
use appencryption::plugins::aws_v1::kms::{AwsKms, StandardAwsKmsClient};
use appencryption::crypto::aes256gcm::Aes256Gcm;
use std::collections::HashMap;
use std::sync::Arc;

async fn setup_kms() {
    // Create a map of regions to KMS key ARNs
    let mut region_map = HashMap::new();
    region_map.insert(
        "us-west-2".to_string(),
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );
    region_map.insert(
        "us-east-1".to_string(),
        "arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );
    
    // Create the KMS service with AES-256-GCM encryption
    let kms = AwsKms::from_region_map(
        region_map,
        "us-west-2", // preferred region
        Arc::new(Aes256Gcm::new()),
    ).unwrap();
    
    // Use the KMS service with a session factory
    // let factory = SessionFactory::builder()
    //     .with_key_management_service(Arc::new(kms))
    //     ...
}
```

## DynamoDB Metastore

The DynamoDB metastore allows you to use DynamoDB for storing encryption keys. It supports:

- Standard table configuration
- Global table support with region-specific partitioning
- Custom table names

### Usage

```rust
use appencryption::plugins::aws_v1::metastore::{DynamoDbMetastore, StandardDynamoDbClient};
use std::sync::Arc;

async fn setup_metastore() {
    // Create a basic metastore
    let metastore = DynamoDbMetastore::new_default("us-west-2".to_string()).unwrap();
    
    // Or with global table support
    let global_metastore = DynamoDbMetastore::new_with_global_table(
        "us-west-2".to_string(),
        Some("CustomTableName".to_string()),
    ).unwrap();
    
    // Use the metastore with a session factory
    // let factory = SessionFactory::builder()
    //     .with_metastore(Arc::new(metastore))
    //     ...
}
```

## Advanced Configuration

Both KMS and DynamoDB clients support custom endpoints, which can be useful for testing with localstack:

```rust
use appencryption::plugins::aws_v1::kms::StandardAwsKmsClient;
use appencryption::plugins::aws_v1::metastore::StandardDynamoDbClient;

async fn setup_with_custom_endpoints() {
    // KMS with custom endpoint
    let kms_client = StandardAwsKmsClient::with_endpoint(
        "us-west-2".to_string(),
        "http://localhost:4566".to_string(),
    ).unwrap();
    
    // DynamoDB with custom endpoint
    let dynamo_client = StandardDynamoDbClient::with_endpoint(
        "us-west-2".to_string(),
        "http://localhost:4566".to_string(),
    ).unwrap();
}
```

## AWS v1 vs v2

This integration uses the rusoto crate, which is based on AWS SDK v1 and is now deprecated in favor of the aws-sdk-* crates (v2). The AWS v1 integration is provided for compatibility with existing systems. For new projects, consider using the AWS v2 integration instead.