# AWS SDK v2 Plugins

This directory contains plugin implementations using the AWS SDK v2 for Rust.

## Modules

- `kms`: KMS implementation for encrypting and decrypting system keys
- `metastore`: DynamoDB implementation for storing encrypted keys

## KMS Plugin

The KMS plugin provides an implementation of the `KeyManagementService` trait using AWS KMS with SDK v2.

### Features

- Multi-region support with automatic failover
- Envelope encryption for system keys
- Support for AWS KMS master keys
- Customizable builder pattern for configuration

### Example

```rust
use appencryption::plugins::aws_v2::kms::{AwsKmsBuilder, StandardAwsKmsClient};
use appencryption::crypto::aes256gcm::Aes256Gcm;
use aws_config::from_env;
use aws_sdk_kms::Client as AwsKmsClient;
use std::collections::HashMap;
use std::sync::Arc;

async fn create_kms() -> Result<Arc<dyn KeyManagementService>, Error> {
    // Load AWS config from environment
    let config = from_env().load().await;
    
    // Create AWS KMS clients for different regions
    let primary_region = "us-west-2";
    let primary_client = AwsKmsClient::new(&config);
    let primary_kms_client = StandardAwsKmsClient::new(
        primary_client, 
        primary_region.to_string(),
    );
    
    // Optionally create a replica client for another region
    let replica_region = "us-east-1";
    let replica_config = from_env().region(replica_region).load().await;
    let replica_client = AwsKmsClient::new(&replica_config);
    let replica_kms_client = StandardAwsKmsClient::new(
        replica_client,
        replica_region.to_string(),
    );
    
    // Create ARN map for KMS keys in each region
    let mut arn_map = HashMap::new();
    arn_map.insert(
        primary_region.to_string(),
        "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
    );
    arn_map.insert(
        replica_region.to_string(),
        "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string(),
    );
    
    // Create a crypto implementation
    let crypto = Arc::new(Aes256Gcm::new());
    
    // Build the KMS service
    let kms = AwsKmsBuilder::new(crypto, arn_map)
        .with_preferred_region(primary_region)
        .with_kms_client(primary_region, Arc::new(primary_kms_client))
        .with_kms_client(replica_region, Arc::new(replica_kms_client))
        .build()?;
    
    Ok(Arc::new(kms))
}
```

## DynamoDB Metastore Plugin

The DynamoDB metastore plugin provides an implementation of the `Metastore` trait using AWS DynamoDB with SDK v2.

### Features

- Support for DynamoDB global tables
- Regional failover with automatic health checking
- Configurable table name
- Region suffix support for partition keys
- Customizable builder pattern for configuration

### Example

```rust
use appencryption::plugins::aws_v2::metastore::{DynamoDbMetastore, StandardDynamoDbClient};
use aws_config::from_env;
use aws_sdk_dynamodb::Client as AwsDynamoDbClient;
use std::sync::Arc;

async fn create_metastore() -> Result<Arc<dyn Metastore>, Error> {
    // Load AWS config from environment
    let config = from_env().load().await;
    
    // Create AWS DynamoDB clients for different regions
    let primary_region = "us-west-2";
    let primary_client = AwsDynamoDbClient::new(&config);
    let primary_ddb_client = StandardDynamoDbClient::new(
        primary_client, 
        primary_region.to_string(),
    );
    
    // Optionally create a replica client for another region
    let replica_region = "us-east-1";
    let replica_config = from_env().region(replica_region).load().await;
    let replica_client = AwsDynamoDbClient::new(&replica_config);
    let replica_ddb_client = StandardDynamoDbClient::new(
        replica_client,
        replica_region.to_string(),
    );
    
    // Create DynamoDB metastore with replicas
    let metastore = DynamoDbMetastore::with_replicas(
        Arc::new(primary_ddb_client),
        vec![Arc::new(replica_ddb_client)],
        Some("EncryptionKeys".to_string()), // table name
        true, // use region suffix
        false, // don't prefer region
    );
    
    Ok(Arc::new(metastore))
}
```

## DynamoDB Schema

The DynamoDB metastore uses the following schema:

- Table Name: `EncryptionKey` (default, configurable)
- Partition Key: `Id` (String) - The key ID, optionally with region suffix
- Sort Key: `Created` (Number) - The creation timestamp of the key
- Attributes:
  - `KeyRecord` (Map) - The encrypted key record
    - `Created` (Number) - The creation timestamp
    - `Key` (String) - The encrypted key (base64 encoded)
    - `Revoked` (Boolean, optional) - Whether the key is revoked
    - `ParentKeyMeta` (Map, optional) - The parent key metadata
      - `KeyId` (String) - The parent key ID
      - `Created` (Number) - The parent key creation timestamp