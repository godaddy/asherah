//! Plugin architecture for AWS service integrations
//!
//! This module organizes AWS-specific implementations into plugins that can be conditionally included.
//! Each plugin module corresponds to a specific AWS SDK version and service combination.
//!
//! ## Available Plugins
//!
//! - `aws-v1`: Implementations using AWS SDK v1
//!   - `kms`: AWS KMS implementation
//!   - `metastore`: DynamoDB metastore implementation
//!
//! - `aws-v2`: Implementations using AWS SDK v2
//!   - `kms`: AWS KMS implementation
//!   - `metastore`: DynamoDB metastore implementation
//!
//! ## Feature Flags
//!
//! These plugins are controlled by feature flags:
//!
//! - `aws-v1`: Enables AWS SDK v1 plugins
//! - `aws-v2`: Enables AWS SDK v2 plugins (default)
//! - `aws-kms`: Enables KMS plugins
//! - `aws-dynamodb`: Enables DynamoDB plugins
//!
//! For example, to use AWS SDK v2 with KMS and DynamoDB:
//! ```toml
//! [dependencies]
//! appencryption = { version = "0.1.0", features = ["aws-v2", "aws-kms", "aws-dynamodb"] }
//! ```
//!
//! To use only AWS SDK v1 with KMS:
//! ```toml
//! [dependencies]
//! appencryption = { version = "0.1.0", features = ["aws-v1", "aws-kms"] }
//! ```

pub mod aws_v1;
pub mod aws_v2;
