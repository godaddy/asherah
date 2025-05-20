//! Key Management Service implementations for the application encryption library
//!
//! This module provides implementations for encrypting and decrypting system keys:
//!
//! - Static KMS for testing and development (using a static master key)
//! - AWS KMS integration with multi-region support (available through the plugins module)
//! - Custom KMS implementations can be added by implementing the KeyManagementService trait
//!
//! For AWS KMS implementations, see the `plugins` module:
//! - AWS SDK v1: `plugins::aws_v1::kms`
//! - AWS SDK v2: `plugins::aws_v2::kms`

mod static_kms;

pub use static_kms::StaticKeyManagementService;

// For backward compatibility, re-export the AWS KMS types from the preferred plugin
#[cfg(feature = "aws-v2-kms")]
pub mod aws {
    pub use crate::plugins::aws_v2::kms::*;
}

#[cfg(feature = "aws-v1-kms")]
#[cfg(not(feature = "aws-v2-kms"))]
pub mod aws {
    pub use crate::plugins::aws_v1::kms::*;
}