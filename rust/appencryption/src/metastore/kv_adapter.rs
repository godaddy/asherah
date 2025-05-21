use crate::{
    envelope::EnvelopeKeyRecord,
    Error, Metastore, Result,
};
use async_trait::async_trait;
use serde_json;
use std::fmt::Debug;
use std::marker::PhantomData;
use std::sync::Arc;

use super::kv_store::{CompositeKey, KeyValueStore};

/// An adapter that implements the Metastore trait using any KeyValueStore implementation.
///
/// This adapter handles the conversion between Asherah's specific key/value semantics
/// and a generic key/value store interface, allowing any KeyValueStore implementation
/// to be used as a Metastore.
#[derive(Debug)]
pub struct KeyValueMetastore<KV, K, V> 
where
    KV: KeyValueStore<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync,
    V: From<String> + TryInto<String> + Clone + Send + Sync,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// The underlying key-value store
    store: Arc<KV>,
    _key_type: PhantomData<K>,
    _value_type: PhantomData<V>,
}

impl<KV, K, V> KeyValueMetastore<KV, K, V>
where
    KV: KeyValueStore<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync,
    V: From<String> + TryInto<String> + Clone + Send + Sync,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    /// Creates a new KeyValueMetastore with the given store
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

#[async_trait]
impl<KV, K, V> Metastore for KeyValueMetastore<KV, K, V>
where
    KV: KeyValueStore<Key = K, Value = V> + Send + Sync,
    K: From<CompositeKey> + Send + Sync,
    V: From<String> + TryInto<String> + Clone + Send + Sync,
    <V as TryInto<String>>::Error: std::error::Error + Send + Sync + 'static,
{
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let key = self.to_store_key(id, created);
        
        let result = self.store.get(&key).await
            .map_err(|e| Error::Metastore(format!("Error loading from store: {}", e)))?;
            
        match result {
            Some(value) => {
                let json_str: String = value.try_into()
                    .map_err(|e| Error::Metastore(format!("Error converting value: {}", e)))?;
                
                let record = self.deserialize_record(&json_str)?;
                Ok(Some(record))
            }
            None => Ok(None),
        }
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        // This is a simplified implementation that may not be efficient for all key-value stores
        // For a real implementation, you would want to optimize this based on the specific
        // capabilities of your key-value store (e.g., range queries, prefix filtering)
        
        // For this generic adapter, we're just returning None as a placeholder
        // A proper implementation would scan for keys with the given ID and find the one
        // with the latest timestamp
        
        // Since this is just an example showing how to implement the adapter pattern,
        // we're not providing a complete implementation here
        
        // In a real implementation, you might:
        // 1. Use a range query if supported by the KV store
        // 2. Use a prefix scan with a filter on the id
        // 3. Maintain a separate index of latest keys
        
        Err(Error::Metastore(
            "load_latest not implemented for generic KeyValueMetastore adapter".to_string()
        ))
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let key = self.to_store_key(id, created);
        let json = self.serialize_record(envelope)?;
        let value = V::from(json);
        
        // Only store if the key doesn't already exist
        self.store.put(&key, &value, true).await
            .map_err(|e| Error::Metastore(format!("Error storing in KV store: {}", e)))
    }
}

// A specialized adapter for string-based key-value stores, which is the most common case
pub type StringKeyValueMetastore<KV> = KeyValueMetastore<KV, String, String>;

impl<KV> StringKeyValueMetastore<KV> 
where 
    KV: KeyValueStore<Key = String, Value = String> + Send + Sync
{
    /// Creates a new StringKeyValueMetastore with the given store
    pub fn new_string_store(store: Arc<KV>) -> Self {
        Self::new(store)
    }
    
    /// Specialized key conversion for string-based stores
    fn to_string_key(&self, id: &str, created: i64) -> String {
        CompositeKey::new(id, created).to_string_key()
    }
}

// Implementation for string-based key-value stores
#[async_trait]
impl<KV> Metastore for StringKeyValueMetastore<KV>
where
    KV: KeyValueStore<Key = String, Value = String> + Send + Sync,
{
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let key = self.to_string_key(id, created);
        
        let result = self.store.get(&key).await
            .map_err(|e| Error::Metastore(format!("Error loading from store: {}", e)))?;
            
        match result {
            Some(json_str) => {
                let record = self.deserialize_record(&json_str)?;
                Ok(Some(record))
            }
            None => Ok(None),
        }
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        // For a string-based store, we would likely implement this by scanning
        // all keys with a prefix matching the id
        
        // This is still a placeholder implementation
        Err(Error::Metastore(
            "load_latest not implemented for string-based KeyValueMetastore adapter".to_string()
        ))
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let key = self.to_string_key(id, created);
        let json = self.serialize_record(envelope)?;
        
        // Only store if the key doesn't already exist
        self.store.put(&key, &json, true).await
            .map_err(|e| Error::Metastore(format!("Error storing in KV store: {}", e)))
    }
}