//! Tests for the DynamoDB metastore implementation
//!
//! These tests validate the DynamoDB metastore implementation using mocks.

use super::*;
use crate::envelope::EnvelopeKeyRecord;
use async_trait::async_trait;
use std::sync::Arc;

/// Mock DynamoDB client for testing
#[derive(Debug)]
struct MockDynamoDbClient {
    region: String,
    should_fail: bool,
    items: Vec<DynamoDbItem>,
}

impl MockDynamoDbClient {
    fn new(region: String) -> Self {
        Self {
            region,
            should_fail: false,
            items: Vec::new(),
        }
    }

    fn with_items(region: String, items: Vec<DynamoDbItem>) -> Self {
        Self {
            region,
            should_fail: false,
            items,
        }
    }

    fn with_failure(region: String) -> Self {
        Self {
            region,
            should_fail: true,
            items: Vec::new(),
        }
    }
}

#[async_trait]
impl DynamoDbClient for MockDynamoDbClient {
    async fn get_item(&self, _table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>> {
        if self.should_fail {
            return Err(Error::Metastore("Mock get_item failure".into()));
        }

        // Find the item with matching keys
        for item in &self.items {
            if item.id == key.id && item.created == key.created {
                return Ok(Some(item.clone()));
            }
        }

        Ok(None)
    }

    async fn put_item_if_not_exists(&self, _table_name: &str, _item: DynamoDbItem) -> Result<bool> {
        if self.should_fail {
            return Err(Error::Metastore("Mock put_item_if_not_exists failure".into()));
        }

        // For testing, just return success
        Ok(true)
    }

    async fn query_latest(&self, _table_name: &str, partition_key: &str) -> Result<Vec<DynamoDbItem>> {
        if self.should_fail {
            return Err(Error::Metastore("Mock query_latest failure".into()));
        }

        // Find items with matching partition key
        let mut matching_items = self.items.iter()
            .filter(|item| item.id == partition_key)
            .cloned()
            .collect::<Vec<_>>();

        // Sort by created timestamp in descending order
        matching_items.sort_by(|a, b| b.created.cmp(&a.created));

        Ok(matching_items)
    }

    fn region(&self) -> &str {
        &self.region
    }
}

fn create_test_item(id: &str, created: i64) -> DynamoDbItem {
    DynamoDbItem {
        id: id.to_string(),
        created,
        key_record: DynamoDbEnvelope {
            revoked: None,
            created,
            encrypted_key: base64::encode(b"test_encrypted_key"),
            parent_key_meta: Some(DynamoDbKeyMeta {
                id: "parent_key".to_string(),
                created: created - 1000,
            }),
        },
    }
}

#[tokio::test]
async fn test_dynamodb_metastore_load() {
    // Create a test item
    let test_item = create_test_item("test_key", 1234567890);

    // Create a mock client with the test item
    let client = Arc::new(MockDynamoDbClient::with_items(
        "us-west-2".to_string(),
        vec![test_item],
    ));

    // Create the metastore
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Load the item
    let result = metastore.load("test_key", 1234567890).await.unwrap();

    // Verify the result
    assert!(result.is_some());
    let record = result.unwrap();
    assert_eq!("test_key", record.id);
    assert_eq!(1234567890, record.created);
    assert_eq!(b"test_encrypted_key", &record.encrypted_key[..]);
    assert!(record.parent_key_meta.is_some());
    let parent_meta = record.parent_key_meta.unwrap();
    assert_eq!("parent_key", parent_meta.id);
    assert_eq!(1234567890 - 1000, parent_meta.created);
}

#[tokio::test]
async fn test_dynamodb_metastore_load_not_found() {
    // Create a mock client with no items
    let client = Arc::new(MockDynamoDbClient::new("us-west-2".to_string()));

    // Create the metastore
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Load a non-existent item
    let result = metastore.load("non_existent", 1234567890).await.unwrap();

    // Verify the result is None
    assert!(result.is_none());
}

#[tokio::test]
async fn test_dynamodb_metastore_load_failure() {
    // Create a mock client that will fail
    let client = Arc::new(MockDynamoDbClient::with_failure("us-west-2".to_string()));

    // Create the metastore
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Load should fail
    let result = metastore.load("test_key", 1234567890).await;

    // Verify the error
    assert!(result.is_err());
    let err = result.unwrap_err();
    if let Error::Metastore(msg) = err {
        assert!(msg.contains("Mock get_item failure"));
    } else {
        panic!("Unexpected error type: {:?}", err);
    }
}

#[tokio::test]
async fn test_dynamodb_metastore_load_latest() {
    // Create test items with different timestamps
    let item1 = create_test_item("test_key", 1234567890);
    let item2 = create_test_item("test_key", 1234567891);
    let item3 = create_test_item("test_key", 1234567889);

    // Create a mock client with the test items
    let client = Arc::new(MockDynamoDbClient::with_items(
        "us-west-2".to_string(),
        vec![item1, item2, item3],
    ));

    // Create the metastore
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Load the latest item
    let result = metastore.load_latest("test_key").await.unwrap();

    // Verify the result is the item with the highest timestamp
    assert!(result.is_some());
    let record = result.unwrap();
    assert_eq!("test_key", record.id);
    assert_eq!(1234567891, record.created);
}

#[tokio::test]
async fn test_dynamodb_metastore_store() {
    // Create a mock client
    let client = Arc::new(MockDynamoDbClient::new("us-west-2".to_string()));

    // Create the metastore
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Create a test envelope
    let envelope = EnvelopeKeyRecord {
        id: "test_key".to_string(),
        created: 1234567890,
        encrypted_key: b"test_encrypted_key".to_vec(),
        revoked: None,
        parent_key_meta: Some(crate::envelope::KeyMeta {
            id: "parent_key".to_string(),
            created: 1234567889,
        }),
    };

    // Store the envelope
    let result = metastore.store("test_key", 1234567890, &envelope).await.unwrap();

    // Verify the result
    assert!(result);
}

#[tokio::test]
async fn test_dynamodb_metastore_with_region_suffix() {
    // Create a mock client
    let client = Arc::new(MockDynamoDbClient::new("us-west-2".to_string()));

    // Create the metastore with region suffix
    let metastore = DynamoDbMetastore::new(client, None, true);

    // Verify the region suffix is set
    assert_eq!(Some("us-west-2"), metastore.region_suffix());

    // Verify get_id_with_suffix adds the region
    let id_with_suffix = metastore.get_id_with_suffix("test_key");
    assert_eq!("test_key_us-west-2", id_with_suffix);

    // Verify get_region_suffix returns the right value
    assert_eq!(Some("us-west-2".to_string()), metastore.get_region_suffix());
}

#[tokio::test]
async fn test_dynamodb_metastore_without_region_suffix() {
    // Create a mock client
    let client = Arc::new(MockDynamoDbClient::new("us-west-2".to_string()));

    // Create the metastore without region suffix
    let metastore = DynamoDbMetastore::new(client, None, false);

    // Verify the region suffix is not set
    assert_eq!(None, metastore.region_suffix());

    // Verify get_id_with_suffix doesn't add the region
    let id_with_suffix = metastore.get_id_with_suffix("test_key");
    assert_eq!("test_key", id_with_suffix);

    // Verify get_region_suffix returns None
    assert_eq!(None, metastore.get_region_suffix());
}

#[tokio::test]
async fn test_dynamodb_metastore_custom_table_name() {
    // Create a mock client
    let client = Arc::new(MockDynamoDbClient::new("us-west-2".to_string()));

    // Create the metastore with a custom table name
    let metastore = DynamoDbMetastore::new(client, Some("CustomTable".to_string()), false);

    // Verify the table name is set
    assert_eq!("CustomTable", metastore.table_name());
}