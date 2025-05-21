#![allow(clippy::future_not_send)]

use async_trait::async_trait;
use std::error::Error as StdError;
use std::fmt;

/// A generic Key-Value store trait that requires `Send` futures.
///
/// This trait defines the essential operations for any key-value storage
/// implementation used by Asherah's metastores. Implementors should provide
/// appropriate error handling and ensure thread safety.
///
/// This trait variant requires that all futures returned by its methods implement `Send`,
/// making it suitable for use in fully concurrent async contexts.
///
/// The `Send + Sync` bounds are required because:
/// 1. Metastore implementations are stored in an Arc and shared between threads
/// 2. KeyValueStore implementations are often wrapped in Arc<KV> in the KeyValueMetastore adapter
/// 3. All operations may be called concurrently from different threads in an async context
///
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[async_trait]
pub trait KeyValueStoreSend: Send + Sync + fmt::Debug {
    /// The type of keys used in this store
    type Key: Send + Sync + fmt::Debug;

    /// The type of values stored
    type Value: Send + Sync + fmt::Debug;

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
    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool, Self::Error>;

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

/// A generic Key-Value store trait that allows non-Send futures.
///
/// This trait is identical to `KeyValueStoreSend` but does not require the futures returned
/// by its methods to implement `Send`. This makes it suitable for use in single-threaded
/// contexts or with operations that use stack references or other non-Send data.
///
/// The store itself still needs to be `Send + Sync` so it can be shared between threads,
/// but the futures it returns don't need to satisfy the `Send` bound.
///
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[async_trait(?Send)]
pub trait KeyValueStoreLocal: Send + Sync + fmt::Debug {
    /// The type of keys used in this store
    type Key: Send + Sync + fmt::Debug;

    /// The type of values stored
    type Value: Send + Sync + fmt::Debug;

    /// The error type returned by operations
    type Error: StdError + Send + Sync + 'static;

    /// Gets a value by key (future doesn't need to be Send)
    ///
    /// # Arguments
    /// * `key` - The key to retrieve
    ///
    /// # Returns
    /// * `Ok(Some(value))` - If the key exists and value was retrieved
    /// * `Ok(None)` - If the key doesn't exist
    /// * `Err(e)` - If an error occurred during retrieval
    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error>;

    /// Stores a value with the given key (future doesn't need to be Send)
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
    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool, Self::Error>;

    /// Deletes a value by key (future doesn't need to be Send)
    ///
    /// # Arguments
    /// * `key` - The key to delete
    ///
    /// # Returns
    /// * `Ok(true)` - If a value was deleted
    /// * `Ok(false)` - If no value existed for the key
    /// * `Err(e)` - If an error occurred during deletion
    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error>;

    /// Checks if a key exists (future doesn't need to be Send)
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

/// A trait for key-value stores that support time-to-live (TTL) expiration with Send futures.
///
/// This extends the basic KeyValueStoreSend with TTL capabilities, requiring all futures
/// returned by its methods to implement Send.
///
/// This trait inherits the `Send + Sync` bounds from KeyValueStoreSend, which are necessary
/// for concurrent usage in Asherah's async environment.
///
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[async_trait]
pub trait TtlKeyValueStoreSend: KeyValueStoreSend {
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

/// A trait for key-value stores that support time-to-live (TTL) expiration with non-Send futures.
///
/// This extends the basic KeyValueStoreLocal with TTL capabilities for stores that can't guarantee
/// their futures are Send, such as those using stack references or thread-local storage.
///
/// This trait inherits the `Send + Sync` bounds from KeyValueStoreLocal, which are necessary
/// for concurrent usage in Asherah's async environment, but doesn't require its returned
/// futures to be Send.
///
/// For Rust versions earlier than 1.75, enable the "async-trait-compat" feature
/// to use the async-trait crate for compatibility.
#[async_trait(?Send)]
pub trait TtlKeyValueStoreLocal: KeyValueStoreLocal {
    /// Sets an expiration time on a key (future doesn't need to be Send)
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

    /// Checks if a key is expired (future doesn't need to be Send)
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

/// For backward compatibility: A type alias for KeyValueStoreSend
///
/// This allows existing code that uses KeyValueStore to continue working
/// while making it explicit that the future must be Send
pub type KeyValueStore<K = (), V = (), E = Box<dyn StdError + Send + Sync>> =
    dyn KeyValueStoreSend<Key = K, Value = V, Error = E> + Send + Sync;

/// For backward compatibility: A type alias for TtlKeyValueStoreSend
///
/// This allows existing code that uses TtlKeyValueStore to continue working
/// while making it explicit that the future must be Send
pub type TtlKeyValueStore<K = (), V = (), E = Box<dyn StdError + Send + Sync>> =
    dyn TtlKeyValueStoreSend<Key = K, Value = V, Error = E> + Send + Sync;

/// For backward compatibility: A type alias for KeyValueStoreLocal
///
/// This was previously called LocalKeyValueStore in the earlier approach
pub type LocalKeyValueStore<K = (), V = (), E = Box<dyn StdError + Send + Sync>> =
    dyn KeyValueStoreLocal<Key = K, Value = V, Error = E> + Send + Sync;

/// For backward compatibility: A type alias for TtlKeyValueStoreLocal
///
/// This was previously called LocalTtlKeyValueStore in the earlier approach
pub type LocalTtlKeyValueStore<K = (), V = (), E = Box<dyn StdError + Send + Sync>> =
    dyn TtlKeyValueStoreLocal<Key = K, Value = V, Error = E> + Send + Sync;

/// An adapter that wraps a KeyValueStoreLocal and makes it compatible with KeyValueStoreSend.
///
/// This adapter makes it possible to use a key-value store that returns non-Send futures
/// in a context that requires Send futures. It uses tokio's spawn_blocking mechanism to
/// execute non-Send futures safely in a dedicated thread.
///
/// The approach works by:
/// 1. Creating a dedicated thread with spawn_blocking for each operation
/// 2. Getting the current tokio runtime within that thread
/// 3. Using block_on to execute the non-Send future within that dedicated thread
/// 4. This ensures thread-safety while allowing the use of non-Send futures
///
/// Note: This adaptation incurs a small runtime cost due to thread creation but allows
/// for more flexible code that can work with both Send and non-Send futures.
#[derive(Debug, Clone)]
pub struct SendKeyValueStoreAdapter<KV: KeyValueStoreLocal + Clone> {
    inner: KV,
}

impl<KV: KeyValueStoreLocal + Clone> SendKeyValueStoreAdapter<KV> {
    /// Creates a new adapter wrapping a KeyValueStoreLocal
    pub fn new(inner: KV) -> Self {
        Self { inner }
    }

    /// Gets a reference to the inner KeyValueStoreLocal
    pub fn inner(&self) -> &KV {
        &self.inner
    }
}

#[async_trait]
impl<KV: KeyValueStoreLocal + Clone + Send + Sync + 'static> KeyValueStoreSend
    for SendKeyValueStoreAdapter<KV>
where
    KV::Key: Clone,
    KV::Value: Clone,
    KV::Error: From<Box<dyn std::error::Error + Send + Sync>>,
{
    type Key = KV::Key;
    type Value = KV::Value;
    type Error = KV::Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.get(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // This is a best-effort conversion and may need adaptation based on the error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool, Self::Error> {
        // Clone the key and value since we need to move them into the async block
        let key = key.clone();
        let value = value.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.put(&key, &value, only_if_absent).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.delete(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn exists(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.exists(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }
}

/// An adapter for TtlKeyValueStoreLocal that implements TtlKeyValueStoreSend
///
/// This adapter extends SendKeyValueStoreAdapter to add TTL capabilities. It wraps
/// a TtlKeyValueStoreLocal and makes it compatible with the Send-required TtlKeyValueStoreSend trait.
///
/// Like SendKeyValueStoreAdapter, it uses tokio's spawn_blocking to safely execute non-Send futures
/// in a dedicated thread, ensuring Send compatibility across async boundaries.
#[derive(Debug, Clone)]
pub struct SendTtlKeyValueStoreAdapter<KV: TtlKeyValueStoreLocal + Clone> {
    inner: KV,
}

impl<KV: TtlKeyValueStoreLocal + Clone> SendTtlKeyValueStoreAdapter<KV> {
    /// Creates a new adapter wrapping a TtlKeyValueStoreLocal
    pub fn new(inner: KV) -> Self {
        Self { inner }
    }

    /// Gets a reference to the inner TtlKeyValueStoreLocal
    pub fn inner(&self) -> &KV {
        &self.inner
    }
}

#[async_trait]
impl<KV: TtlKeyValueStoreLocal + Clone + Send + Sync + 'static> KeyValueStoreSend
    for SendTtlKeyValueStoreAdapter<KV>
where
    KV::Key: Clone,
    KV::Value: Clone,
    KV::Error: From<Box<dyn std::error::Error + Send + Sync>>,
{
    type Key = KV::Key;
    type Value = KV::Value;
    type Error = KV::Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.get(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // This is a best-effort conversion and may need adaptation based on the error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool, Self::Error> {
        // Clone the key and value since we need to move them into the async block
        let key = key.clone();
        let value = value.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.put(&key, &value, only_if_absent).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.delete(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn exists(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.exists(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }
}

#[async_trait]
impl<KV: TtlKeyValueStoreLocal + Clone + Send + Sync + 'static> TtlKeyValueStoreSend
    for SendTtlKeyValueStoreAdapter<KV>
where
    KV::Key: Clone,
    KV::Value: Clone,
    KV::Error: From<Box<dyn std::error::Error + Send + Sync>>,
{
    async fn expire(&self, key: &Self::Key, ttl_seconds: i64) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.expire(&key, ttl_seconds).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }

    async fn is_expired(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Clone the key since we need to move it into the async block
        let key = key.clone();
        let inner = self.inner.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current thread's runtime
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { inner.is_expired(&key).await })
        })
        .await
        // Convert JoinError to the store's error type
        // Handle JoinError - convert to the KV store's error type
        .map_err(|e| KV::Error::from(Box::new(e)))?
    }
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

// Add From<CompositeKey> for String implementation for string-based key-value stores
impl From<CompositeKey> for String {
    fn from(key: CompositeKey) -> Self {
        key.to_string_key()
    }
}
