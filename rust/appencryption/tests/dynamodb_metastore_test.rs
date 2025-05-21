#[cfg(feature = "aws-v2-dynamodb")]
mod tests {
    use appencryption::envelope::{EnvelopeKeyRecord, KeyMeta};
    use appencryption::error::{Error, Result};
    use appencryption::plugins::aws_v2::metastore::{
        DynamoDbClient, DynamoDbItem, DynamoDbKey, DynamoDbMetastore,
    };
    use appencryption::Metastore;
    use async_trait::async_trait;
    use std::collections::HashMap;
    use std::sync::{Arc, Mutex};
    use tokio_test::block_on;

    // A mock DynamoDB client for testing
    #[derive(Debug)]
    struct MockDynamoDbClient {
        region: String,
        healthy: bool,
        items: Mutex<HashMap<String, DynamoDbItem>>, // key is "id:created"
    }

    impl MockDynamoDbClient {
        fn new(region: &str, healthy: bool) -> Self {
            Self {
                region: region.to_string(),
                healthy,
                items: Mutex::new(HashMap::new()),
            }
        }

        fn add_item(&self, item: DynamoDbItem) {
            let key = format!("{}:{}", item.id, item.created);
            let mut items = self.items.lock().unwrap();
            items.insert(key, item);
        }
    }

    #[async_trait]
    impl DynamoDbClient for MockDynamoDbClient {
        async fn get_item(
            &self,
            _table_name: &str,
            key: DynamoDbKey,
        ) -> Result<Option<DynamoDbItem>> {
            if !self.healthy {
                return Err(Error::Metastore("Mock client is unhealthy".into()));
            }

            let items = self.items.lock().unwrap();
            let item_key = format!("{}:{}", key.id, key.created);

            Ok(items.get(&item_key).cloned())
        }

        async fn put_item_if_not_exists(
            &self,
            _table_name: &str,
            item: DynamoDbItem,
        ) -> Result<bool> {
            if !self.healthy {
                return Err(Error::Metastore("Mock client is unhealthy".into()));
            }

            let mut items = self.items.lock().unwrap();
            let item_key = format!("{}:{}", item.id, item.created);

            if items.contains_key(&item_key) {
                return Ok(false);
            }

            items.insert(item_key, item);
            Ok(true)
        }

        async fn query_latest(
            &self,
            _table_name: &str,
            partition_key: &str,
        ) -> Result<Vec<DynamoDbItem>> {
            if !self.healthy {
                return Err(Error::Metastore("Mock client is unhealthy".into()));
            }

            let items = self.items.lock().unwrap();

            // Find the latest item for the partition key
            let mut latest_item = None;
            let mut latest_created = -1;

            for (_, item) in items.iter() {
                if item.id == partition_key && item.created > latest_created {
                    latest_item = Some(item.clone());
                    latest_created = item.created;
                }
            }

            if let Some(item) = latest_item {
                Ok(vec![item])
            } else {
                Ok(Vec::new())
            }
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

    #[test]
    fn test_dynamodb_metastore_single_region() {
        block_on(async {
            // Create a mock DynamoDB client
            let client = Arc::new(MockDynamoDbClient::new("us-west-2", true));

            // Create DynamoDB metastore
            let metastore = DynamoDbMetastore::new(client, Some("TestTable".to_string()), false);

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
            let stored = metastore
                .store("test-key", 1234567890, &key_record)
                .await
                .expect("Failed to store key");
            assert!(stored, "Key should have been stored");

            // Load the key by exact ID and timestamp
            let loaded = metastore
                .load("test-key", 1234567890)
                .await
                .expect("Failed to load key");
            assert!(loaded.is_some(), "Key should have been loaded");

            let loaded_key = loaded.unwrap();
            assert_eq!(key_record.id, loaded_key.id);
            assert_eq!(key_record.created, loaded_key.created);
            assert_eq!(key_record.encrypted_key, loaded_key.encrypted_key);

            // Load the latest key
            let latest = metastore
                .load_latest("test-key")
                .await
                .expect("Failed to load latest key");
            assert!(latest.is_some(), "Latest key should have been loaded");

            let latest_key = latest.unwrap();
            assert_eq!(key_record.id, latest_key.id);
        });
    }

    #[test]
    fn test_dynamodb_metastore_multi_region() {
        block_on(async {
            // Create mock DynamoDB clients
            let primary = Arc::new(MockDynamoDbClient::new("us-west-2", true));
            let replica1 = Arc::new(MockDynamoDbClient::new("us-east-1", true));
            let replica2 = Arc::new(MockDynamoDbClient::new("eu-west-1", false));

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
            let stored = metastore
                .store("test-key", 1234567890, &key_record)
                .await
                .expect("Failed to store key");
            assert!(stored, "Key should have been stored");

            // Load the key by exact ID and timestamp
            let loaded = metastore
                .load("test-key", 1234567890)
                .await
                .expect("Failed to load key");
            assert!(loaded.is_some(), "Key should have been loaded");

            // Check region suffix
            assert_eq!(Some("us-west-2"), metastore.region_suffix());
        });
    }
}
