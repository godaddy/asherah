use crate::{envelope::EnvelopeKeyRecord, Error, Metastore, Result};
use async_trait::async_trait;
use serde_json;
// The SendWrapper is no longer needed as we use tokio::task::spawn_blocking instead
// use send_wrapper::SendWrapper;
use std::fmt;
use std::marker::PhantomData;
use std::sync::Arc;
use tokio;

use super::kv_store::{CompositeKey, KeyValueStoreLocal, KeyValueStoreSend};

/// An adapter that implements the Metastore trait using any KeyValueStoreSend implementation.
///
/// This adapter handles the conversion between Asherah's specific key/value semantics
/// and a generic key/value store interface, allowing any KeyValueStoreSend implementation
/// to be used as a Metastore.
///
/// Note that KeyValueStoreSend already has Send + Sync bounds and returns Send futures,
/// which aligns with the requirements of the Metastore trait.
///
/// This is the recommended adapter for most use cases, as it ensures full Send compatibility
/// across async boundaries.
#[derive(Debug)]
pub struct KeyValueMetastoreForSend<KV, K, V>
where
    KV: KeyValueStoreSend<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync + fmt::Debug,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// The underlying key-value store
    store: Arc<KV>,
    _key_type: PhantomData<K>,
    _value_type: PhantomData<V>,
}

impl<KV, K, V> KeyValueMetastoreForSend<KV, K, V>
where
    KV: KeyValueStoreSend<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync + fmt::Debug,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// Creates a new KeyValueMetastoreForSend with the given store
    pub fn new(store: Arc<KV>) -> Self {
        Self {
            store,
            _key_type: PhantomData,
            _value_type: PhantomData,
        }
    }

    /// Converts a CompositeKey to the store's key type
    fn to_store_key(&self, id: &str, created: i64) -> K {
        K::from(CompositeKey::new(id, created))
    }

    /// Converts a JSON string to an EnvelopeKeyRecord
    fn deserialize_record(&self, json: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json)
            .map_err(|e| Error::Metastore(format!("Error deserializing envelope record: {}", e)))
    }

    /// Converts an EnvelopeKeyRecord to a JSON string
    fn serialize_record(&self, record: &EnvelopeKeyRecord) -> Result<String> {
        serde_json::to_string(record)
            .map_err(|e| Error::Metastore(format!("Error serializing envelope record: {}", e)))
    }
}

/// Implements the Metastore trait for KeyValueMetastoreForSend.
///
/// Since KeyValueStoreSend already ensures that its futures are Send,
/// this implementation can directly await on the store's methods.
#[async_trait]
impl<KV, K, V> Metastore for KeyValueMetastoreForSend<KV, K, V>
where
    KV: KeyValueStoreSend<Key = K, Value = V> + Send + Sync + 'static,
    K: From<CompositeKey> + Send + Sync + fmt::Debug + 'static,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug + 'static,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let key = self.to_store_key(id, created);
        let store = &self.store;

        // Create a thread-local task that runs the future
        let result = async move {
            // The future returned by KeyValueStoreSend should be Send-compatible,
            // but we're using move + async block to ensure it's Send
            store.get(&key).await
        }
        .await
        .map_err(|e| Error::Metastore(format!("Error loading from store: {}", e)))?;

        match result {
            Some(value) => {
                let json_str: String = value
                    .try_into()
                    .map_err(|e| Error::Metastore(format!("Error converting value: {}", e)))?;

                let record = self.deserialize_record(&json_str)?;
                Ok(Some(record))
            }
            None => Ok(None),
        }
    }

    async fn load_latest(&self, _id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        // This is a simplified implementation that may not be efficient for all key-value stores
        // For a real implementation, you would want to optimize this based on the specific
        // capabilities of your key-value store (e.g., range queries, prefix filtering)

        // In a real implementation, you might:
        // 1. Use a range query if supported by the KV store
        // 2. Use a prefix scan with a filter on the id
        // 3. Maintain a separate index of latest keys

        Err(Error::Metastore(
            "load_latest not implemented for generic KeyValueMetastoreForSend adapter".to_string(),
        ))
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let key = self.to_store_key(id, created);
        let json = self.serialize_record(envelope)?;
        let value = V::from(json);
        let store = &self.store;

        // Create a thread-local task that runs the future
        async move {
            // The future returned by KeyValueStoreSend should be Send-compatible,
            // but we're using move + async block to ensure it's Send
            store.put(&key, &value, true).await
        }
        .await
        .map_err(|e| Error::Metastore(format!("Error storing in KV store: {}", e)))
    }
}

/// An adapter that implements the Metastore trait using any KeyValueStoreLocal implementation.
///
/// This adapter handles the conversion between Asherah's specific key/value semantics
/// and a generic key/value store interface, allowing any KeyValueStoreLocal implementation
/// to be used as a Metastore.
///
/// Note that KeyValueStoreLocal already has Send + Sync bounds for the implementor type,
/// but its future return types are not required to be Send. This adapter uses tokio's
/// spawn_blocking to bridge the gap and make the futures Send-compatible.
///
/// This approach works by:
/// 1. Creating a dedicated thread with spawn_blocking for each operation
/// 2. Getting the current tokio runtime within that thread
/// 3. Using block_on to execute the non-Send future within that dedicated thread
/// 4. This ensures thread-safety while allowing the use of non-Send futures
///
/// Use this adapter when you have a key-value store that can't provide Send-compatible futures,
/// such as one that uses stack references or other non-Send data across await points.
///
/// Note: This approach incurs a small performance overhead from thread creation but enables
/// broader compatibility with various key-value store implementations.
#[derive(Debug)]
pub struct KeyValueMetastoreForLocal<KV, K, V>
where
    KV: KeyValueStoreLocal<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync + fmt::Debug,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// The underlying key-value store
    store: Arc<KV>,
    _key_type: PhantomData<K>,
    _value_type: PhantomData<V>,
}

impl<KV, K, V> KeyValueMetastoreForLocal<KV, K, V>
where
    KV: KeyValueStoreLocal<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync + fmt::Debug,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// Creates a new KeyValueMetastoreForLocal with the given store
    pub fn new(store: Arc<KV>) -> Self {
        Self {
            store,
            _key_type: PhantomData,
            _value_type: PhantomData,
        }
    }

    /// Converts a CompositeKey to the store's key type
    fn to_store_key(&self, id: &str, created: i64) -> K {
        K::from(CompositeKey::new(id, created))
    }

    /// Converts a JSON string to an EnvelopeKeyRecord
    fn deserialize_record(&self, json: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json)
            .map_err(|e| Error::Metastore(format!("Error deserializing envelope record: {}", e)))
    }

    /// Converts an EnvelopeKeyRecord to a JSON string
    fn serialize_record(&self, record: &EnvelopeKeyRecord) -> Result<String> {
        serde_json::to_string(record)
            .map_err(|e| Error::Metastore(format!("Error serializing envelope record: {}", e)))
    }
}

/// Implements the Metastore trait for KeyValueMetastoreForLocal.
///
/// Since KeyValueStoreLocal doesn't guarantee that its futures are Send,
/// this implementation uses tokio::task::spawn_blocking to safely execute
/// non-Send futures in a dedicated thread, ensuring Send compatibility.
#[async_trait]
impl<KV, K, V> Metastore for KeyValueMetastoreForLocal<KV, K, V>
where
    KV: KeyValueStoreLocal<Key = K, Value = V> + Send + Sync + 'static,
    K: From<CompositeKey> + Send + Sync + fmt::Debug + 'static,
    V: From<String> + TryInto<String> + Clone + Send + Sync + fmt::Debug + 'static,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    // This implementation handles non-Send futures from the underlying KV store
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let key = self.to_store_key(id, created);
        let store = self.store.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        // This works because spawn_blocking creates a dedicated thread
        // where we can safely await the non-Send future
        let result = tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current
            // thread's runtime to safely await our non-Send future
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { store.get(&key).await })
        })
        .await
        // Handle JoinError from spawn_blocking
        .map_err(|e| Error::Metastore(format!("Thread join error: {}", e)))?
        // Handle KV store error
        .map_err(|e| Error::Metastore(format!("Error loading from store: {}", e)))?;

        match result {
            Some(value) => {
                let json_str: String = value
                    .try_into()
                    .map_err(|e| Error::Metastore(format!("Error converting value: {}", e)))?;

                let record = self.deserialize_record(&json_str)?;
                Ok(Some(record))
            }
            None => Ok(None),
        }
    }

    async fn load_latest(&self, _id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        // For now, we'll still return an error
        // A true implementation would require additional methods on the key-value store
        // to support querying by prefix or range, which isn't part of the basic interface
        Err(Error::Metastore(
            "load_latest not implemented for generic KeyValueMetastoreForLocal adapter".to_string(),
        ))

        // A proper implementation would look something like this:
        /*
        let id_string = _id.to_string();
        let store = self.store.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        tokio::task::spawn_blocking(move || {
            // Inside this thread, we can safely use non-Send futures
            let rt = tokio::runtime::Handle::current();

            // Here we would need a way to query by prefix or range
            // This would require extending the KeyValueStore traits
            rt.block_on(async {
                // Example pseudocode for a range query
                // store.get_by_prefix(&id_string).await
            })
        })
        .await
        .map_err(|e| Error::Metastore(format!("Thread join error: {}", e)))?
        .map_err(|e| Error::Metastore(format!("Error loading latest from store: {}", e)))?
        */
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let key = self.to_store_key(id, created);
        let json = self.serialize_record(envelope)?;
        let value = V::from(json);
        let store = self.store.clone();

        // Use tokio's spawn_blocking to safely execute the non-Send future
        // This works because spawn_blocking creates a dedicated thread
        // where we can safely await the non-Send future
        tokio::task::spawn_blocking(move || {
            // We're now in a dedicated thread, so we can use the current
            // thread's runtime to safely await our non-Send future
            let rt = tokio::runtime::Handle::current();
            rt.block_on(async { store.put(&key, &value, true).await })
        })
        .await
        // Handle JoinError from spawn_blocking
        .map_err(|e| Error::Metastore(format!("Thread join error: {}", e)))?
        // Handle KV store error
        .map_err(|e| Error::Metastore(format!("Error storing in KV store: {}", e)))
    }
}

// For backward compatibility: Type aliases for the new adapter types

/// Type alias for KeyValueMetastoreForSend with String keys and values
pub type StringKeyValueMetastoreForSend<KV> = KeyValueMetastoreForSend<KV, String, String>;

/// Type alias for KeyValueMetastoreForLocal with String keys and values
pub type StringKeyValueMetastoreForLocal<KV> = KeyValueMetastoreForLocal<KV, String, String>;

/// Helper function for string-based key-value stores
#[allow(dead_code)]
fn to_string_key(id: &str, created: i64) -> String {
    CompositeKey::new(id, created).to_string_key()
}

// Additional implementations for string-based key-value stores
impl<KV> StringKeyValueMetastoreForSend<KV>
where
    KV: KeyValueStoreSend<Key = String, Value = String> + Send + Sync,
{
    /// Creates a new StringKeyValueMetastoreForSend with the given store
    pub fn new_string_store(store: Arc<KV>) -> Self {
        Self::new(store)
    }
}

impl<KV> StringKeyValueMetastoreForLocal<KV>
where
    KV: KeyValueStoreLocal<Key = String, Value = String> + Send + Sync,
{
    /// Creates a new StringKeyValueMetastoreForLocal with the given store
    pub fn new_string_store(store: Arc<KV>) -> Self {
        Self::new(store)
    }
}

// For backward compatibility
pub type KeyValueMetastore<KV, K, V> = KeyValueMetastoreForSend<KV, K, V>;
pub type StringKeyValueMetastore<KV> = StringKeyValueMetastoreForSend<KV>;
