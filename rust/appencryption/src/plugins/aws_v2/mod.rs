//! AWS SDK v2 plugin implementations
//!
//! This module contains implementations for AWS services using the AWS SDK v2.
//! These implementations are conditionally compiled using the `aws-v2` feature flag.

#[cfg(feature = "aws-kms")]
pub mod kms;

#[cfg(feature = "aws-dynamodb")]
pub mod metastore;
