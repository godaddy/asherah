#[cfg(feature = "async-trait-compat")]
use async_trait::async_trait;
use std::error::Error as StdError;
use std::fmt::Debug;

/// A generic Key-Value store trait.
///
/// This trait defines the essential operations for any key-value storage
/// implementation used by Asherah's metastores. Implementors should provide
/// appropriate error handling and ensure thread safety.
/// 
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[cfg_attr(feature = "async-trait-compat", async_trait)]
pub trait KeyValueStore: Send + Sync {
    /// The type of keys used in this store
    type Key: Send + Sync;
    
    /// The type of values stored
    type Value: Send + Sync;
    
    /// The error type returned by operations
    type Error: StdError + Send + Sync + 'static;

    /// Gets a value by key
    ///
    /// # Arguments
    /// * `key` - The key to retrieve
    ///
    /// # Returns
    /// * `Ok(Some(value))` - If the key exists and value was retrieved
    /// * `Ok(None)` - If the key doesn't exist
    /// * `Err(e)` - If an error occurred during retrieval
    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error>;

    /// Stores a value with the given key
    ///
    /// # Arguments
    /// * `key` - The key to store the value under
    /// * `value` - The value to store
    /// * `only_if_absent` - If true, only store if the key doesn't already exist
    ///
    /// # Returns
    /// * `Ok(true)` - If the value was stored
    /// * `Ok(false)` - If the key already existed and `only_if_absent` was true
    /// * `Err(e)` - If an error occurred during storage
    async fn put(&self, key: &Self::Key, value: &Self::Value, only_if_absent: bool) -> Result<bool, Self::Error>;

    /// Deletes a value by key
    ///
    /// # Arguments
    /// * `key` - The key to delete
    ///
    /// # Returns
    /// * `Ok(true)` - If a value was deleted
    /// * `Ok(false)` - If no value existed for the key
    /// * `Err(e)` - If an error occurred during deletion
    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error>;

    /// Checks if a key exists
    ///
    /// Default implementation uses `get` but implementors can optimize this
    /// for stores that have a more efficient existence check.
    ///
    /// # Arguments
    /// * `key` - The key to check
    ///
    /// # Returns
    /// * `Ok(true)` - If the key exists
    /// * `Ok(false)` - If the key doesn't exist
    /// * `Err(e)` - If an error occurred during the check
    async fn exists(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        Ok(self.get(key).await?.is_some())
    }
}

/// A trait for key-value stores that support time-to-live (TTL) expiration.
///
/// This extends the basic KeyValueStore with TTL capabilities.
/// 
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[cfg_attr(feature = "async-trait-compat", async_trait)]
pub trait TtlKeyValueStore: KeyValueStore {
    /// Sets an expiration time on a key
    ///
    /// # Arguments
    /// * `key` - The key to set expiration for
    /// * `ttl_seconds` - Time-to-live in seconds
    ///
    /// # Returns
    /// * `Ok(true)` - If the expiration was set
    /// * `Ok(false)` - If the key doesn't exist
    /// * `Err(e)` - If an error occurred while setting expiration
    async fn expire(&self, key: &Self::Key, ttl_seconds: i64) -> Result<bool, Self::Error>;

    /// Checks if a key is expired
    ///
    /// Note: Most implementations handle expiration checks internally during get/put operations,
    /// so this method may not need to be called directly in most cases.
    ///
    /// # Arguments
    /// * `key` - The key to check
    ///
    /// # Returns
    /// * `Ok(true)` - If the key exists and is expired
    /// * `Ok(false)` - If the key doesn't exist or isn't expired
    /// * `Err(e)` - If an error occurred during the check
    async fn is_expired(&self, key: &Self::Key) -> Result<bool, Self::Error>;
}

/// A composite key type for Asherah's metastore operations.
///
/// This represents the standard key format used by metastores,
/// combining an ID string with a creation timestamp.
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct CompositeKey {
    pub id: String,
    pub created: i64,
}

impl CompositeKey {
    /// Creates a new composite key
    pub fn new(id: &str, created: i64) -> Self {
        Self {
            id: id.to_string(),
            created,
        }
    }

    /// Formats the key as a string for storage systems that use string keys
    pub fn to_string_key(&self) -> String {
        format!("{}_{}", self.id, self.created)
    }
}

impl From<(String, i64)> for CompositeKey {
    fn from((id, created): (String, i64)) -> Self {
        Self { id, created }
    }
}

impl From<(&str, i64)> for CompositeKey {
    fn from((id, created): (&str, i64)) -> Self {
        Self {
            id: id.to_string(),
            created,
        }
    }
}