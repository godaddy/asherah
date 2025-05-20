//! DynamoDB metastore implementation using AWS SDK v2
//!
//! This module provides a complete implementation of the `Metastore` trait using DynamoDB with AWS SDK v2.
//! It includes support for global tables, regional failover, and health checking.

mod client;
mod builder;

pub use client::{DynamoDbClient, DynamoDbKey, DynamoDbItem, DynamoDbEnvelope, DynamoDbKeyMeta, DynamoDbMetastore};
pub use builder::DynamoDbClientBuilder;