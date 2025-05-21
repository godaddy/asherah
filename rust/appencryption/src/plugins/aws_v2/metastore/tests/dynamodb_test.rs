use super::*;
use crate::plugins::aws_v2::metastore::{DynamoDbClient, DynamoDbKey, DynamoDbItem, DynamoDbEnvelope, DynamoDbMetastore};
use crate::envelope::{EnvelopeKeyRecord, KeyMeta};
use crate::Metastore;
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;

// Mock DynamoDB client for testing
struct MockDynamoDbClient {
    region: String,
    store: Arc<Mutex<HashMap<String, HashMap<i64, DynamoDbItem>>>>,
    healthy: bool,
}

impl MockDynamoDbClient {
    fn new(region: impl Into<String>) -> Self {
        Self {
            region: region.into(),
            store: Arc::new(Mutex::new(HashMap::new())),
            healthy: true,
        }
    }

    fn with_health(mut self, healthy: bool) -> Self {
        self.healthy = healthy;
        self
    }
}

#[async_trait]
impl DynamoDbClient for MockDynamoDbClient {
    async fn get_item(&self, _table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>> {
        let store = self.store.lock().unwrap();

        // Simulate lookup by partition key and sort key
        if let Some(partition) = store.get(&key.id) {
            if let Some(item) = partition.get(&key.created) {
                return Ok(Some(item.clone()));
            }
        }

        Ok(None)
    }

    async fn put_item_if_not_exists(&self, _table_name: &str, item: DynamoDbItem) -> Result<bool> {
        let mut store = self.store.lock().unwrap();

        // Get or create partition map
        let partition = store.entry(item.id.clone()).or_insert_with(HashMap::new);

        // Check if key already exists
        if partition.contains_key(&item.created) {
            return Ok(false);
        }

        // Insert the item
        partition.insert(item.created, item);

        Ok(true)
    }

    async fn query_latest(&self, _table_name: &str, partition_key: &str) -> Result<Vec<DynamoDbItem>> {
        let store = self.store.lock().unwrap();

        // Find partition
        if let Some(partition) = store.get(partition_key) {
            // Sort by created timestamp (descending)
            let mut items: Vec<_> = partition.values().cloned().collect();
            items.sort_by(|a, b| b.created.cmp(&a.created));

            return Ok(items);
        }

        Ok(Vec::new())
    }

    fn region(&self) -> &str {
        &self.region
    }

    fn is_healthy(&self) -> bool {
        self.healthy
    }

    async fn health_check(&self) -> Result<bool> {
        Ok(self.healthy)
    }
}

#[tokio::test]
async fn test_dynamodb_metastore_single_region() {
    // Create a mock DynamoDB client
    let client = Arc::new(MockDynamoDbClient::new("us-west-2"));

    // Create DynamoDB metastore
    let metastore = DynamoDbMetastore::new(
        client,
        Some("TestTable".to_string()),
        false,
    );

    // Create a test key record
    let key_record = EnvelopeKeyRecord {
        id: "test-key".to_string(),
        created: 1234567890,
        encrypted_key: b"encrypted-key-data".to_vec(),
        revoked: None,
        parent_key_meta: Some(KeyMeta {
            id: "parent-key".to_string(),
            created: 1234567800,
        }),
    };

    // Store the key
    let stored = metastore.store("test-key", 1234567890, &key_record).await.expect("Failed to store key");
    assert!(stored, "Key should have been stored");

    // Load the key by exact ID and timestamp
    let loaded = metastore.load("test-key", 1234567890).await.expect("Failed to load key");
    assert!(loaded.is_some(), "Key should have been loaded");

    let loaded_key = loaded.unwrap();
    assert_eq!(key_record.id, loaded_key.id);
    assert_eq!(key_record.created, loaded_key.created);
    assert_eq!(key_record.encrypted_key, loaded_key.encrypted_key);

    // Load the latest key
    let latest = metastore.load_latest("test-key").await.expect("Failed to load latest key");
    assert!(latest.is_some(), "Latest key should have been loaded");

    let latest_key = latest.unwrap();
    assert_eq!(key_record.id, latest_key.id);
}

#[tokio::test]
async fn test_dynamodb_metastore_multi_region() {
    // Create mock DynamoDB clients
    let primary = Arc::new(MockDynamoDbClient::new("us-west-2"));
    let replica1 = Arc::new(MockDynamoDbClient::new("us-east-1"));
    let replica2 = Arc::new(MockDynamoDbClient::new("eu-west-1").with_health(false));

    // Create DynamoDB metastore with replicas
    let metastore = DynamoDbMetastore::with_replicas(
        primary,
        vec![replica1, replica2],
        Some("TestTable".to_string()),
        true, // use region suffix
        true, // prefer region
    );

    // Create a test key record
    let key_record = EnvelopeKeyRecord {
        id: "test-key".to_string(),
        created: 1234567890,
        encrypted_key: b"encrypted-key-data".to_vec(),
        revoked: None,
        parent_key_meta: Some(KeyMeta {
            id: "parent-key".to_string(),
            created: 1234567800,
        }),
    };

    // Store the key
    let stored = metastore.store("test-key", 1234567890, &key_record).await.expect("Failed to store key");
    assert!(stored, "Key should have been stored");

    // Load the key by exact ID and timestamp
    let loaded = metastore.load("test-key", 1234567890).await.expect("Failed to load key");
    assert!(loaded.is_some(), "Key should have been loaded");

    // Check region suffix
    assert_eq!(Some("us-west-2".to_string()), metastore.get_region_suffix());
}