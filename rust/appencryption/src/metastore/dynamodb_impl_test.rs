#[cfg(test)]
mod tests {
    use super::*;
    use crate::metastore::dynamodb::{DynamoDbClient, DynamoDbEnvelope, DynamoDbItem, DynamoDbKey, DynamoDbKeyMeta};
    use crate::metastore::dynamodb::MultiRegionClient;
    use crate::Metastore;
    use async_trait::async_trait;
    use std::sync::{Arc, Mutex};
    use std::time::Duration;
    use std::collections::HashMap;
    use tokio_test::block_on;

    // A mock DynamoDB client for testing
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
        async fn get_item(&self, _table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>> {
            if !self.healthy {
                return Err(Error::Metastore("Mock client is unhealthy".into()));
            }
            
            let items = self.items.lock().unwrap();
            let item_key = format!("{}:{}", key.id, key.created);
            
            Ok(items.get(&item_key).cloned())
        }
        
        async fn put_item_if_not_exists(&self, _table_name: &str, item: DynamoDbItem) -> Result<bool> {
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
        
        async fn query_latest(&self, _table_name: &str, partition_key: &str) -> Result<Vec<DynamoDbItem>> {
            if !self.healthy {
                return Err(Error::Metastore("Mock client is unhealthy".into()));
            }
            
            let items = self.items.lock().unwrap();
            
            // Find the latest item for the partition key
            let mut latest_item = None;
            let mut latest_created = -1;
            
            for (key, item) in items.iter() {
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
    fn test_multi_region_client_healthy_clients() {
        // Create clients
        let primary = Arc::new(MockDynamoDbClient::new("us-west-2", true));
        let replica1 = Arc::new(MockDynamoDbClient::new("us-east-1", true));
        let replica2 = Arc::new(MockDynamoDbClient::new("eu-west-1", false));
        let replicas = vec![replica1.clone(), replica2.clone()];
        
        // Create multi-region client
        let client = MultiRegionClient::new(
            primary.clone(),
            replicas,
            Duration::from_secs(30),
            3,
        );
        
        // Get healthy clients
        let healthy_clients = client.healthy_clients();
        
        // Primary and replica1 should be healthy
        assert_eq!(healthy_clients.len(), 2);
        assert!(healthy_clients.contains(&primary));
        assert!(healthy_clients.contains(&replica1));
        assert!(!healthy_clients.contains(&replica2));
    }

    #[test]
    fn test_multi_region_client_execute_async() {
        block_on(async {
            // Create clients
            let primary = Arc::new(MockDynamoDbClient::new("us-west-2", false)); // Primary is unhealthy
            let replica = Arc::new(MockDynamoDbClient::new("us-east-1", true));  // Replica is healthy
            let replicas = vec![replica.clone()];
            
            // Add test data to the replica
            let test_item = DynamoDbItem {
                id: "test-key".to_string(),
                created: 123456789,
                key_record: DynamoDbEnvelope {
                    revoked: None,
                    created: 123456789,
                    encrypted_key: "test-encrypted-key".to_string(),
                    parent_key_meta: None,
                },
            };
            replica.add_item(test_item.clone());
            
            // Create multi-region client
            let client = MultiRegionClient::new(
                primary.clone(),
                replicas,
                Duration::from_secs(30),
                3,
            );
            
            // Execute operation async - should use the replica since primary is unhealthy
            let result = client.execute_async(|c| async move {
                c.get_item("test-table", DynamoDbKey {
                    id: "test-key".to_string(),
                    created: 123456789,
                }).await
            }).await;
            
            // Check result
            assert!(result.is_ok());
            let item = result.unwrap();
            assert!(item.is_some());
            let item = item.unwrap();
            assert_eq!(item.id, "test-key");
            assert_eq!(item.created, 123456789);
            assert_eq!(item.key_record.encrypted_key, "test-encrypted-key");
        });
    }

    #[test]
    fn test_dynamodb_metastore_with_region_suffix() {
        block_on(async {
            // Create mock client
            let client = Arc::new(MockDynamoDbClient::new("us-west-2", true));
            
            // Create metastore with region suffix
            let metastore = crate::metastore::dynamodb::DynamoDbMetastore::with_replicas(
                client.clone(),
                Vec::new(),
                Some("test-table".to_string()),
                true,   // use region suffix
                false,  // don't prefer region
            );
            
            // Check region suffix
            assert_eq!(metastore.region_suffix(), Some("us-west-2"));
            
            // Test get_id_with_suffix
            let id = "test-key";
            let id_with_suffix = metastore.get_id_with_suffix(id);
            
            // Without prefer_region, the suffix isn't actually added to the ID
            assert_eq!(id_with_suffix, id);
        });
    }

    #[test]
    fn test_dynamodb_metastore_with_prefer_region() {
        block_on(async {
            // Create mock client
            let client = Arc::new(MockDynamoDbClient::new("us-west-2", true));
            
            // Create metastore with region suffix and prefer_region
            let metastore = crate::metastore::dynamodb::DynamoDbMetastore::with_replicas(
                client.clone(),
                Vec::new(),
                Some("test-table".to_string()),
                true,   // use region suffix
                true,   // prefer region
            );
            
            // Check region suffix
            assert_eq!(metastore.region_suffix(), Some("us-west-2"));
            
            // Test get_id_with_suffix
            let id = "test-key";
            let id_with_suffix = metastore.get_id_with_suffix(id);
            
            // With prefer_region, the suffix should be added to the ID
            assert_eq!(id_with_suffix, "test-key_us-west-2");
        });
    }

    #[test]
    fn test_dynamodb_metastore_store_and_load() {
        block_on(async {
            // Create mock client
            let client = Arc::new(MockDynamoDbClient::new("us-west-2", true));
            
            // Create metastore
            let metastore = crate::metastore::dynamodb::DynamoDbMetastore::new(
                client.clone(),
                Some("test-table".to_string()),
                false,  // don't use region suffix
            );
            
            // Create test envelope
            let envelope = crate::EnvelopeKeyRecord {
                id: "test-key".to_string(),
                created: 123456789,
                encrypted_key: vec![1, 2, 3, 4, 5],
                revoked: None,
                parent_key_meta: None,
            };
            
            // Store envelope
            let store_result = metastore.store("test-key", 123456789, &envelope).await;
            assert!(store_result.is_ok());
            assert!(store_result.unwrap());
            
            // Load envelope
            let load_result = metastore.load("test-key", 123456789).await;
            assert!(load_result.is_ok());
            let loaded_envelope = load_result.unwrap();
            assert!(loaded_envelope.is_some());
            let loaded_envelope = loaded_envelope.unwrap();
            
            // Check loaded envelope matches original
            assert_eq!(loaded_envelope.id, envelope.id);
            assert_eq!(loaded_envelope.created, envelope.created);
            assert_eq!(loaded_envelope.encrypted_key, envelope.encrypted_key);
            assert_eq!(loaded_envelope.revoked, envelope.revoked);
            assert_eq!(loaded_envelope.parent_key_meta.is_none(), envelope.parent_key_meta.is_none());
            
            // Test load_latest
            let load_latest_result = metastore.load_latest("test-key").await;
            assert!(load_latest_result.is_ok());
            let loaded_latest = load_latest_result.unwrap();
            assert!(loaded_latest.is_some());
            let loaded_latest = loaded_latest.unwrap();
            
            // Check loaded latest matches original
            assert_eq!(loaded_latest.id, envelope.id);
            assert_eq!(loaded_latest.created, envelope.created);
            assert_eq!(loaded_latest.encrypted_key, envelope.encrypted_key);
            assert_eq!(loaded_latest.revoked, envelope.revoked);
            assert_eq!(loaded_latest.parent_key_meta.is_none(), envelope.parent_key_meta.is_none());
        });
    }
}