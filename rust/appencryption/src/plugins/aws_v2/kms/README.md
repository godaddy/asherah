# AWS KMS v2 for Asherah

This module provides a Rust implementation of Asherah's `KeyManagementService` trait using AWS KMS with AWS SDK v2.

## Features

- Envelope encryption for secure key management
- Multi-region support with configurable failover
- Fluent builder pattern for flexible configuration
- Customizable retry logic and timeouts
- Support for custom endpoints (useful for testing)
- Comprehensive metrics integration

## Usage

### Basic Usage

```rust
use std::collections::HashMap;
use std::sync::Arc;
use asherah::crypto::aes256gcm::Aes256Gcm;
use asherah::plugins::aws_v2::kms::new_aws_kms;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create an ARN map with KMS keys for each region
    let mut arn_map = HashMap::new();
    arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    
    // Create AEAD crypto implementation
    let crypto = Arc::new(Aes256Gcm::new());
    
    // Create the AWS KMS service
    let kms = new_aws_kms(crypto, "us-west-2", arn_map).await?;
    
    // Use KMS to encrypt and decrypt keys
    let my_key = b"sensitive encryption key".to_vec();
    let encrypted = kms.encrypt_key(&my_key).await?;
    let decrypted = kms.decrypt_key(&encrypted).await?;
    
    assert_eq!(my_key, decrypted);
    
    Ok(())
}
```

### Advanced Configuration with Builder Pattern

```rust
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;
use asherah::crypto::aes256gcm::Aes256Gcm;
use asherah::plugins::aws_v2::kms::AwsKmsBuilder;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create an ARN map with KMS keys for multiple regions
    let mut arn_map = HashMap::new();
    arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    arn_map.insert("us-east-1".to_string(), "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string());
    
    // Create AEAD crypto implementation
    let crypto = Arc::new(Aes256Gcm::new());
    
    // Use the builder for advanced configuration
    let kms = AwsKmsBuilder::new(crypto, arn_map)
        // Set preferred region for multi-region failover
        .with_preferred_region("us-west-2")
        
        // Configure timeout for AWS operations
        .with_timeout(Duration::from_secs(5))
        
        // Configure retry behavior (3 retries with 100ms base delay)
        .with_retry_config(3, 100)
        
        // Set custom endpoint for testing with localstack
        .with_endpoint("us-west-2", "http://localhost:4566")
        
        // Build the KMS service
        .build()
        .await?;
    
    // Use KMS as before...
    
    Ok(())
}
```

### Custom AWS Configuration

```rust
use std::collections::HashMap;
use std::sync::Arc;
use asherah::crypto::aes256gcm::Aes256Gcm;
use asherah::plugins::aws_v2::kms::AwsKmsBuilder;
use aws_config::{BehaviorVersion, Region};
use aws_types::credentials::{ProvideCredentials, Credentials};

struct StaticCredentialsProvider {
    credentials: Credentials,
}

impl ProvideCredentials for StaticCredentialsProvider {
    fn provide_credentials<'a>(&'a self) -> impl Future<Output = Result<Credentials, Error>> + Send + 'a {
        futures::future::ready(Ok(self.credentials.clone()))
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create custom AWS config
    let credentials = Credentials::new(
        "AKIAIOSFODNN7EXAMPLE",
        "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
        None, 
        None,
        "static",
    );
    
    let provider = StaticCredentialsProvider { credentials };
    
    let config = aws_config::from_env()
        .credentials_provider(provider)
        .region(Region::new("us-west-2"))
        .behavior_version(BehaviorVersion::latest())
        .load()
        .await;
    
    // Create ARN map
    let mut arn_map = HashMap::new();
    arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    
    // Create AEAD crypto
    let crypto = Arc::new(Aes256Gcm::new());
    
    // Use the builder with custom AWS config
    let kms = AwsKmsBuilder::new(crypto, arn_map)
        .with_aws_config(config)
        .build()
        .await?;
    
    // Use KMS as before...
    
    Ok(())
}
```

## Advanced Features

### Multi-Region Operations

The AWS KMS implementation supports multi-region key management. When encrypting a key, it will:

1. Generate a data key (DEK) in the preferred region
2. Encrypt the Asherah key with that data key
3. Encrypt the data key in each configured region
4. Bundle everything into a JSON envelope

When decrypting, it will:

1. Extract the JSON envelope
2. Try to decrypt the data key using the preferred region
3. If that fails, try the remaining regions in order
4. Use the decrypted data key to decrypt the Asherah key

### Custom KMS Factory

For testing or advanced customization, you can provide a custom KMS factory function:

```rust
use std::collections::HashMap;
use std::sync::Arc;
use asherah::crypto::aes256gcm::Aes256Gcm;
use asherah::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
use aws_config::SdkConfig;
use aws_sdk_kms::Client as AwsSdkKmsClient;

// Custom factory function for creating KMS clients
let factory = |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
    // Create custom client with special configuration
    let client = AwsSdkKmsClient::new(&config);
    let region = config.region().unwrap().to_string();
    
    Arc::new(MyCustomKmsClient::new(client, region))
};

// Create builder with custom factory
let mut arn_map = HashMap::new();
arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());

let crypto = Arc::new(Aes256Gcm::new());

let kms = AwsKmsBuilder::new(crypto, arn_map)
    .with_kms_factory(factory)
    .build()
    .await?;
```

## Error Handling

The AWS KMS implementation includes comprehensive error handling:

- Validation errors for incorrect builder configuration
- Network and AWS service errors with descriptive messages
- Proper handling of region failover for multi-region setups

All errors are wrapped in Asherah's `Error::Kms` variant, making integration with existing error handling straightforward.

## Metrics

The implementation includes metrics for key operations:

- `ael.kms.aws.encryptkey` - Time to encrypt a key
- `ael.kms.aws.decryptkey` - Time to decrypt a key
- `ael.kms.aws.generatedatakey.<region>` - Time to generate a data key in a specific region

These metrics are compatible with the metrics crate and can be collected and reported by your metrics system of choice.