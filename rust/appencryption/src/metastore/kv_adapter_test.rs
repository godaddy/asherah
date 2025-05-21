use super::kv_adapter::{KeyValueMetastoreForLocal, KeyValueMetastoreForSend};
use super::kv_store::{KeyValueStoreLocal, KeyValueStoreSend};
use crate::envelope::{EnvelopeKeyRecord, KeyMeta};
use crate::Metastore;
use crate::{Error, Result};
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};

// A simple in-memory key-value store for testing that returns Send futures
#[derive(Debug, Clone)]
struct SimpleMemoryKvSend {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl SimpleMemoryKvSend {
    fn new() -> Self {
        SimpleMemoryKvSend {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait]
impl KeyValueStoreSend for SimpleMemoryKvSend {
    type Key = String;
    type Value = String;
    type Error = Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>> {
        let data = self.data.lock().unwrap();
        Ok(data.get(key).cloned())
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        if only_if_absent && data.contains_key(key) {
            Ok(false)
        } else {
            data.insert(key.clone(), value.clone());
            Ok(true)
        }
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        Ok(data.remove(key).is_some())
    }
}

// A simple in-memory key-value store that returns non-Send futures
#[derive(Debug, Clone)]
struct SimpleMemoryKvLocal {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl SimpleMemoryKvLocal {
    fn new() -> Self {
        SimpleMemoryKvLocal {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait(?Send)]
impl KeyValueStoreLocal for SimpleMemoryKvLocal {
    type Key = String;
    type Value = String;
    type Error = Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>> {
        let data = self.data.lock().unwrap();
        Ok(data.get(key).cloned())
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        if only_if_absent && data.contains_key(key) {
            Ok(false)
        } else {
            data.insert(key.clone(), value.clone());
            Ok(true)
        }
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        Ok(data.remove(key).is_some())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_kv_metastore_for_send() {
        // Test the KeyValueMetastoreForSend adapter
        let kv_store = Arc::new(SimpleMemoryKvSend::new());
        let metastore = KeyValueMetastoreForSend::<_, String, String>::new(kv_store);

        // Create a test envelope key record
        let created = chrono::Utc::now().timestamp();
        let record = EnvelopeKeyRecord {
            id: "test_keyid".to_string(),
            revoked: None,
            created,
            encrypted_key: vec![1, 2, 3, 4, 5],
            parent_key_meta: Some(KeyMeta::new("parent_id".to_string(), created)),
        };

        // Test storing and loading
        let result = metastore.store("test_id", created, &record).await;
        assert!(result.is_ok());
        assert!(result.unwrap());

        let loaded = metastore.load("test_id", created).await;
        assert!(loaded.is_ok());
        assert!(loaded.unwrap().is_some());
    }

    #[tokio::test]
    async fn test_kv_metastore_for_local() {
        // Test the KeyValueMetastoreForLocal adapter
        let kv_store = Arc::new(SimpleMemoryKvLocal::new());
        let metastore = KeyValueMetastoreForLocal::<_, String, String>::new(kv_store);

        // Create a test envelope key record
        let created = chrono::Utc::now().timestamp();
        let record = EnvelopeKeyRecord {
            id: "test_keyid".to_string(),
            revoked: None,
            created,
            encrypted_key: vec![1, 2, 3, 4, 5],
            parent_key_meta: Some(KeyMeta::new("parent_id".to_string(), created)),
        };

        // Test storing and loading
        let result = metastore.store("test_id", created, &record).await;
        assert!(result.is_ok());
        assert!(result.unwrap());

        let loaded = metastore.load("test_id", created).await;
        assert!(loaded.is_ok());
        assert!(loaded.unwrap().is_some());
    }
}
