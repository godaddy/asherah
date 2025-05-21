use appencryption::envelope::{EnvelopeKeyRecord, KeyMeta};
use appencryption::metastore::kv_adapter::{KeyValueMetastoreForLocal, KeyValueMetastoreForSend};
use appencryption::metastore::kv_store::{KeyValueStoreLocal, KeyValueStoreSend};
use appencryption::{Error, Metastore, Result};
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};

// Test implementation that provides Send-compatible futures
#[derive(Debug, Clone)]
struct SendKeyValueStore {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl SendKeyValueStore {
    fn new() -> Self {
        Self {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait]
impl KeyValueStoreSend for SendKeyValueStore {
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

// Test implementation that provides non-Send futures (but simulates the interface)
#[derive(Debug, Clone)]
struct LocalKeyValueStore {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl LocalKeyValueStore {
    fn new() -> Self {
        Self {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait(?Send)]
impl KeyValueStoreLocal for LocalKeyValueStore {
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

async fn test_metastore_send_capability<M: Metastore + Send + Sync>(metastore: &M) -> Result<()> {
    let created = chrono::Utc::now().timestamp();
    let record = EnvelopeKeyRecord {
        id: "test_key".to_string(),
        revoked: None,
        created,
        encrypted_key: vec![1, 2, 3, 4, 5],
        parent_key_meta: Some(KeyMeta::new("parent".to_string(), created)),
    };

    // Store the record
    let stored = metastore.store("test_partition", created, &record).await?;
    assert!(stored);

    // Load the record back
    let loaded = metastore.load("test_partition", created).await?;
    assert!(loaded.is_some());

    let loaded_record = loaded.unwrap();
    assert_eq!(loaded_record.encrypted_key, record.encrypted_key);

    Ok(())
}

#[tokio::test]
async fn test_send_kv_metastore_across_threads() {
    let kv_store = Arc::new(SendKeyValueStore::new());
    let metastore = Arc::new(KeyValueMetastoreForSend::<_, String, String>::new(kv_store));

    // Test that the metastore can be moved across threads
    let metastore_clone = metastore.clone();
    let handle =
        tokio::spawn(async move { test_metastore_send_capability(&*metastore_clone).await });

    let result = handle.await.unwrap();
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_local_kv_metastore_with_spawn_blocking() {
    let kv_store = Arc::new(LocalKeyValueStore::new());
    let metastore = Arc::new(KeyValueMetastoreForLocal::<_, String, String>::new(
        kv_store,
    ));

    // Test that the local metastore with spawn_blocking bridge works across threads
    let metastore_clone = metastore.clone();
    let handle =
        tokio::spawn(async move { test_metastore_send_capability(&*metastore_clone).await });

    let result = handle.await.unwrap();
    assert!(result.is_ok());
}

#[tokio::test]
async fn test_concurrent_access() {
    let kv_store = Arc::new(SendKeyValueStore::new());
    let metastore = Arc::new(KeyValueMetastoreForSend::<_, String, String>::new(kv_store));

    let mut handles = Vec::new();

    // Spawn multiple tasks that concurrently access the metastore
    for i in 0..10 {
        let metastore_clone = metastore.clone();
        let handle = tokio::spawn(async move {
            let created = chrono::Utc::now().timestamp() + i;
            let record = EnvelopeKeyRecord {
                id: format!("test_key_{}", i),
                revoked: None,
                created,
                encrypted_key: vec![i as u8; 5],
                parent_key_meta: Some(KeyMeta::new("parent".to_string(), created)),
            };

            let stored = metastore_clone
                .store(&format!("partition_{}", i), created, &record)
                .await?;
            assert!(stored);

            let loaded = metastore_clone
                .load(&format!("partition_{}", i), created)
                .await?;
            assert!(loaded.is_some());

            Ok::<(), Error>(())
        });
        handles.push(handle);
    }

    // Wait for all tasks to complete
    for handle in handles {
        let result = handle.await.unwrap();
        assert!(result.is_ok());
    }
}
