//! AWS SDK v1 plugin implementations
//!
//! This module contains implementations for AWS services using the AWS SDK v1 (rusoto crates).
//! These implementations are conditionally compiled using the `aws-v1`, `aws-v1-kms`, and `aws-v1-dynamodb` feature flags.
//!
//! The AWS v1 integration provides:
//! - KMS integration for key management service
//! - DynamoDB integration for the metastore
//!
//! These implementations are functionally equivalent to the AWS v2 implementations,
//! but use the older rusoto crate instead of the newer aws-sdk-* crates.
//! They are provided for compatibility with existing systems that already use rusoto.
//!
//! **Note:** The rusoto crate is deprecated. For new projects, consider using the AWS v2 integration instead.
//!
//! See the README.md file in this directory for more information and usage examples.

#[cfg(feature = "aws-kms")]
pub mod kms;

#[cfg(feature = "aws-dynamodb")]
pub mod metastore;

#[cfg(test)]
mod tests {
    #[test]
    fn test_aws_v1_feature_flags() {
        // This is just a placeholder test to verify the feature flags are working.
        // The actual functionality is tested in the respective submodules.
        assert!(true);
    }
}
