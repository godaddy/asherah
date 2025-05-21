#![allow(clippy::future_not_send)]

//! # Application Encryption Library
//!
//! A library for application-level envelope encryption.
//!
//! `appencryption` provides application-level envelope encryption. It manages a hierarchy of keys
//! (System Keys, Intermediate Keys, Data Row Keys), implements key rotation and expiration policies
//! defined in `CryptoPolicy`, supports key caching with various strategies, and uses a `Metastore`
//! for persistent storage of encrypted keys.
//!
//! The library implements an "inline" key rotation strategy. This means new cryptographic keys
//! are generated on-demand when existing keys expire (as per `CryptoPolicy`) or are not found
//! during an encryption operation. Explicit 'queued' or background key rotation, as found in
//! some other implementations, is not part of the current design. Key expiration and rotation
//! are primarily driven by the `CryptoPolicy` settings and the lifecycle management within the
//! key caching and loading mechanisms.
//!
//! ## Basic Usage
//!
//! ```rust,no_run
//! use appencryption::policy::CryptoPolicy;
//! use appencryption::kms::StaticKeyManagementService;
//! use appencryption::metastore::InMemoryMetastore;
//! use appencryption::session::{SessionFactory, Session};
//! use securememory::protected_memory::DefaultSecretFactory;
//! use std::sync::Arc;
//!
//! # async fn example() -> Result<(), Box<dyn std::error::Error>> {
//! // Create dependencies
//! let policy = CryptoPolicy::new();
//! let master_key = vec![0u8; 32]; // In production, use a real master key
//! let kms = Arc::new(StaticKeyManagementService::new(master_key));
//! let metastore = Arc::new(InMemoryMetastore::new());
//! let secret_factory = Arc::new(DefaultSecretFactory::new());
//!
//! // Create session factory
//! let factory = SessionFactory::new(
//!     "service",
//!     "product",
//!     policy,
//!     kms,
//!     metastore,
//!     secret_factory,
//!     vec![], // plugins
//! );
//!
//! // Create session for a partition
//! let session = factory.session("user123").await?;
//!
//! // Encrypt data
//! let data = b"secret data".to_vec();
//! let encrypted = session.encrypt(&data).await?;
//!
//! // Decrypt data
//! let decrypted = session.decrypt(&encrypted).await?;
//! assert_eq!(data, decrypted);
//!
//! // Close session when done
//! session.close().await?;
//! # Ok(())
//! # }
//! ```
//!
//! ## Using with Persistence
//!
//! You can use the persistence API to easily store and load encrypted data:
//!
//! ```rust,no_run
//! use appencryption::policy::CryptoPolicy;
//! use appencryption::kms::StaticKeyManagementService;
//! use appencryption::persistence::MemoryMetastore;
//! use appencryption::session::{SessionFactory, Session};
//! use appencryption::persistence::{LoaderFn, StorerFn};
//! use appencryption::envelope::DataRowRecord;
//! use securememory::protected_memory::DefaultSecretFactory;
//! use std::sync::{Arc, Mutex};
//! use std::collections::HashMap;
//!
//! # async fn persistence_example() -> Result<(), Box<dyn std::error::Error>> {
//! // Create dependencies
//! let policy = CryptoPolicy::new();
//! let master_key = vec![0u8; 32]; // In production, use a real master key
//! let kms = Arc::new(StaticKeyManagementService::new(master_key));
//! let metastore = Arc::new(MemoryMetastore::new());
//! let secret_factory = Arc::new(DefaultSecretFactory::new());
//!
//! // Create session factory
//! let factory = SessionFactory::new(
//!     "service",
//!     "product",
//!     policy,
//!     kms,
//!     metastore,
//!     secret_factory,
//!     vec![], // plugins
//! );
//!
//! // Create session for a partition
//! let session = factory.session("user123").await?;
//!
//! // Create a simple in-memory data store using a HashMap
//! let data_store = Arc::new(Mutex::new(HashMap::<String, DataRowRecord>::new()));
//!
//! // Create a unique key for this record
//! let record_key = "record_123".to_string();
//!
//! // Create a StorerFn adapter using a closure
//! let store_fn = {
//!     let data_store = data_store.clone();
//!     let record_key = record_key.clone();
//!
//!     StorerFn::new(move |drr: &DataRowRecord| {
//!         let mut store = data_store.lock().unwrap();
//!         store.insert(record_key.clone(), drr.clone());
//!         Ok(record_key.clone())
//!     })
//! };
//!
//! // Create a LoaderFn adapter using a closure
//! let load_fn = {
//!     let data_store = data_store.clone();
//!
//!     LoaderFn::new(move |key: &String| {
//!         let store = data_store.lock().unwrap();
//!         Ok(store.get(key).cloned())
//!     })
//! };
//!
//! // Data to encrypt
//! let data = b"secret data".to_vec();
//!
//! // Store the data with encryption
//! let stored_key = session.store(&data, store_fn).await?;
//!
//! // Load and decrypt the data
//! let loaded_data = session.load(&stored_key, load_fn).await?;
//! assert_eq!(data, loaded_data);
//!
//! // Close session when done
//! session.close().await?;
//! # Ok(())
//! # }
//! ```
//!
//! ## Using SQL Metastore
//!
//! ```rust,ignore
//! use appencryption::policy::CryptoPolicy;
//! use appencryption::plugins::aws_v2::kms::{AwsKmsClient, AwsKmsBuilder};
//! use appencryption::persistence::SqlMetastore;
//! use appencryption::session::SessionFactory;
//! use securememory::protected_memory::DefaultSecretFactory;
//! use std::sync::Arc;
//!
//! # async fn sql_example() -> Result<(), Box<dyn std::error::Error>> {
//! // Create an AWS KMS client
//! let kms = AwsKmsBuilder::new()
//!     .with_region("us-west-2")
//!     .with_key_id("alias/my-key")
//!     .build()
//!     .await?;
//!
//! // In a real application, you would implement SqlClient for your SQL database
//! // This is just a placeholder for the example
//! let sql_client = Arc::new(YourSqlClientImplementation::new(/* connection params */));
//!
//! // Create an SQL metastore
//! let metastore = Arc::new(SqlMetastore::new(
//!     sql_client,
//!     appencryption::persistence::SqlMetastoreDbType::MySql
//! ));
//!
//! // Create session factory
//! let factory = SessionFactory::new(
//!     "service",
//!     "product",
//!     CryptoPolicy::new(),
//!     Arc::new(kms),
//!     metastore,
//!     Arc::new(DefaultSecretFactory::new()),
//!     vec![], // plugins
//! );
//!
//! // Use the factory to create sessions as needed
//! # struct YourSqlClientImplementation {}
//! # impl YourSqlClientImplementation {
//! #     fn new() -> Self { Self {} }
//! # }
//! # use async_trait::async_trait;
//! # use chrono::{DateTime, Utc};
//! # #[async_trait]
//! # impl appencryption::persistence::SqlClient for YourSqlClientImplementation {
//! #     async fn load_key(&self, _: &str, _: &str, _: DateTime<Utc>) -> appencryption::Result<Option<String>> { Ok(None) }
//! #     async fn load_latest_key(&self, _: &str, _: &str) -> appencryption::Result<Option<String>> { Ok(None) }
//! #     async fn store_key(&self, _: &str, _: &str, _: DateTime<Utc>, _: &str) -> appencryption::Result<bool> { Ok(true) }
//! # }
//! # Ok(())
//! # }
//! ```

pub mod cache;
pub mod crypto;
pub mod envelope;
pub mod error;
pub mod key;
pub mod kms;
pub mod log;
pub mod metastore;
pub mod metrics;
pub mod partition;
pub mod persistence;
pub mod policy;
pub mod session;
pub mod session_cache;
pub mod util;

// Plugin architecture for AWS service integrations
pub mod plugins;

// Re-export key types
pub use crate::cache::{Cache, CacheBuilder, CachePolicy};
pub use crate::envelope::{DataRowRecord, EnvelopeKeyRecord, KeyMeta};
pub use crate::error::{Error, Result};
pub use crate::log::{debug_enabled, set_logger, Logger, StdoutLogger};
pub use crate::metrics::{disable_metrics, metrics_enabled, set_metrics_provider, MetricsProvider};
pub use crate::partition::{DefaultPartition, Partition, SuffixedPartition};
pub use crate::policy::CryptoPolicy;
pub use crate::session::{Session, SessionFactory};
pub use crate::session_cache::{SessionCache, SharedEncryption};

/// Size of AES-256 key in bytes
pub const AES256_KEY_SIZE: usize = 32;

use async_trait::async_trait;
use std::fmt;

/// Encryption interface for encrypting and decrypting data
#[async_trait]
pub trait Encryption: Send + Sync + fmt::Debug {
    /// Encrypts a payload and returns a data row record
    async fn encrypt_payload(&self, data: &[u8]) -> Result<DataRowRecord>;

    /// Decrypts a data row record and returns the original data
    async fn decrypt_data_row_record(&self, drr: &DataRowRecord) -> Result<Vec<u8>>;

    /// Closes the encryption session and releases resources
    async fn close(&self) -> Result<()>;

    /// Convert to Any for downcasting
    fn as_any(&self) -> &(dyn std::any::Any + Send + Sync);
}

/// Key Management Service interface for encrypting and decrypting system keys
#[async_trait]
pub trait KeyManagementService: Send + Sync + fmt::Debug {
    /// Encrypts a key using the master key
    async fn encrypt_key(&self, key: &[u8]) -> Result<Vec<u8>>;

    /// Decrypts a key using the master key
    async fn decrypt_key(&self, encrypted_key: &[u8]) -> Result<Vec<u8>>;
}

/// Metastore interface for storing and retrieving encrypted keys
#[async_trait]
pub trait Metastore: Send + Sync + fmt::Debug {
    /// Loads a specific key by ID and creation timestamp
    #[allow(clippy::future_not_send)]
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>>;

    /// Loads the latest key for a given ID
    #[allow(clippy::future_not_send)]
    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>>;

    /// Stores a key in the metastore
    ///
    /// Returns true if the key was stored, false if a key already exists
    #[allow(clippy::future_not_send)]
    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool>;
}

/// AEAD (Authenticated Encryption with Associated Data) interface
pub trait Aead: Send + Sync + fmt::Debug {
    /// Encrypts data using the provided key
    fn encrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>>;

    /// Decrypts data using the provided key
    fn decrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>>;
}

/// Loader interface for loading data from a persistence store
#[async_trait]
pub trait Loader: Send + Sync {
    /// Type of the key used to look up the data
    type Key: Send + Sync;

    /// Loads a data row record from the store using the provided key
    async fn load(&self, key: &Self::Key) -> Result<Option<DataRowRecord>>;
}

/// Storer interface for storing data in a persistence store
#[async_trait]
pub trait Storer: Send + Sync {
    /// Type of the key returned after storing the data
    type Key;

    /// Stores a data row record in the store and returns a key for future lookup
    async fn store(&self, drr: &DataRowRecord) -> Result<Self::Key>;
}
mod cache_test;
