//! Persistence implementations for the application encryption library
//!
//! This module provides various implementations for storing encrypted keys and data.
//! It includes:
//!
//! - SQL metastore for relational databases
//! - In-memory metastore for testing
//! - Function adapters for custom persistence backends

mod functions;
mod memory;
mod sql;

pub use functions::{LoaderFn, StorerFn};
pub use memory::MemoryMetastore;
pub use sql::{SqlClient, SqlMetastore, SqlMetastoreDbType};
