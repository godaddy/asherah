//! Persistence implementations for the application encryption library
//!
//! This module provides various implementations for storing encrypted keys and data.
//! It includes:
//!
//! - SQL metastore for relational databases
//! - In-memory metastore for testing
//! - Function adapters for custom persistence backends

mod memory;
mod sql;
mod functions;

pub use memory::MemoryMetastore;
pub use sql::{SqlMetastore, SqlMetastoreDbType, SqlClient};
pub use functions::{LoaderFn, StorerFn};