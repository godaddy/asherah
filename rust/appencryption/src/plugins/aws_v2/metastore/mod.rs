//! DynamoDB metastore implementation using AWS SDK v2
//!
//! This module provides a complete implementation of the `Metastore` trait using DynamoDB with AWS SDK v2.
//! It includes support for global tables, regional failover, and health checking.

mod builder;
mod client;

pub use builder::DynamoDbClientBuilder;
pub use client::{
    DynamoDbClient, DynamoDbEnvelope, DynamoDbItem, DynamoDbKey, DynamoDbKeyMeta, DynamoDbMetastore,
};
