//! Metastore implementations for the application encryption library
//!
//! This module provides implementations for storing encrypted keys in various backends:
//!
//! - In-memory metastore for testing and development
//! - DynamoDB metastore for AWS integration (available through the plugins module)
//! - MySQL metastore for SQL database integration (requires the 'mysql' feature)
//! - PostgreSQL metastore for SQL database integration (requires the 'postgres' feature)
//! - SQL Server metastore for SQL database integration (requires the 'mssql' feature)
//! - ADO.NET metastore for .NET database integration (placeholder implementation)
//! - Other metastore implementations can be added by implementing the Metastore trait
//!
//! For AWS DynamoDB implementations, see the `plugins` module:
//! - AWS SDK v1: `plugins::aws_v1::metastore`
//! - AWS SDK v2: `plugins::aws_v2::metastore`

pub mod memory;

#[cfg(feature = "mysql")]
mod mysql;

#[cfg(feature = "postgres")]
mod postgres;

mod mssql;

#[cfg(feature = "oracle")]
mod oracle;

mod ado;

pub use memory::InMemoryMetastore;

#[cfg(feature = "mysql")]
pub use mysql::MySqlMetastore;

#[cfg(feature = "postgres")]
pub use postgres::PostgresMetastore;

pub use mssql::MssqlMetastore;

#[cfg(feature = "oracle")]
pub use oracle::OracleMetastore;

pub use ado::AdoMetastore;

// For backward compatibility, re-export the DynamoDB types from the preferred plugin
#[cfg(feature = "aws-v2-dynamodb")]
pub use crate::plugins::aws_v2::metastore::*;

#[cfg(feature = "aws-v1-dynamodb")]
#[cfg(not(feature = "aws-v2-dynamodb"))]
pub use crate::plugins::aws_v1::metastore::*;
