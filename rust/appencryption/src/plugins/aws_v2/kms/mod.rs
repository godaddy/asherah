//! AWS KMS implementation using AWS SDK v2
//!
//! This module provides a complete implementation of the `KeyManagementService` trait using AWS KMS with SDK v2.
//! It includes support for multi-region key management and envelope encryption.
//!
//! # Examples
//!
//! ```
//! use std::collections::HashMap;
//! use std::sync::Arc;
//! use appencryption::crypto::Aes256GcmAead;
//! use appencryption::plugins::aws_v2::kms::{AwsKmsBuilder, new_aws_kms};
//!
//! #[tokio::main]
//! async fn main() -> Result<(), Box<dyn std::error::Error>> {
//!     // Create an ARN map with KMS keys for multiple regions
//!     let mut arn_map = HashMap::new();
//!     arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
//!     arn_map.insert("us-east-1".to_string(), "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string());
//!
//!     // Create AEAD crypto
//!     let crypto = Arc::new(Aes256GcmAead::new());
//!
//!     // Method 1: Using the builder pattern with advanced options
//!     let kms1 = AwsKmsBuilder::new(crypto.clone(), arn_map.clone())
//!         .with_preferred_region("us-west-2")
//!         .with_retry_config(3, 100) // 3 retries with 100ms base delay
//!         .build()
//!         .await?;
//!
//!     // Method 2: Using the convenience function for simple cases
//!     let kms2 = new_aws_kms(crypto.clone(), "us-west-2", arn_map.clone()).await?;
//!
//!     Ok(())
//! }
//! ```

mod client;
mod builder;

#[cfg(test)]
mod tests {
    mod builder_test;
    mod kms_test;
    mod aws_kms_builder_unit_test;
}

pub use client::{AwsKms, AwsKmsClient, GenerateDataKeyResponse, StandardAwsKmsClient};
pub use builder::{AwsKmsBuilder, KmsFactory, new_aws_kms};