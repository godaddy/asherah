// DynamoDB test context, using a simple in-memory metastore for testing

use appencryption::{
    crypto::Aes256GcmAead, envelope::EnvelopeKeyRecord, metastore::InMemoryMetastore, Aead,
    KeyMeta, Metastore,
};
use chrono::{DateTime, Utc};
use hex;
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};

// This is a simplified test context that uses in-memory storage
// In a real implementation, you'd use DynamoDB with localstack or real AWS
pub struct DynamoDbTestContext {
    metastore: Arc<InMemoryMetastore>,
    table_name: String,
    created_timestamp: i64,
}

impl DynamoDbTestContext {
    pub fn new(created_timestamp: Option<i64>) -> Self {
        let metastore = Arc::new(InMemoryMetastore::new());
        let table_name = "encryption_key_test".to_string();

        let created = created_timestamp.unwrap_or_else(|| {
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .expect("Time went backwards")
                .as_secs() as i64
        });

        Self {
            metastore,
            table_name,
            created_timestamp: created,
        }
    }

    pub fn new_metastore(&self, _use_region_suffix: bool) -> Arc<dyn Metastore> {
        // In a real implementation we would create a real DynamoDB client with region suffixes
        // For testing purposes, we're using the in-memory metastore
        self.metastore.clone() as Arc<dyn Metastore>
    }

    pub async fn seed_db(&self) {
        // Seed the database with some test data
        // This would add some predefined encryption keys to the DynamoDB table
        let test_key_id = format!("_IK_{}_{}_{}", "partition_1", "service", "product");

        let system_key_id = format!("_SK_{}_{}", "service", "product");

        let parent_key_meta = KeyMeta {
            id: system_key_id,
            created: self.created_timestamp,
        };

        // Create a properly encrypted intermediate key
        let crypto = Aes256GcmAead::new();
        let dummy_key = vec![0u8; 32]; // Dummy AES-256 key

        // Use the same static key that's used in the test
        let static_key_hex = "0000000000000000000000000000000000000000000000000000000000000000";
        let system_key = hex::decode(static_key_hex).expect("Invalid hex key");

        let encrypted_key = crypto
            .encrypt(&dummy_key, &system_key)
            .expect("Failed to encrypt key");

        let ik_envelope = EnvelopeKeyRecord {
            id: test_key_id.clone(),
            created: self.created_timestamp,
            encrypted_key,
            revoked: None,
            parent_key_meta: Some(parent_key_meta),
        };

        // Store the key using the metastore
        let metastore = self.new_metastore(false);
        metastore
            .store(&test_key_id, self.created_timestamp, &ik_envelope)
            .await
            .expect("Failed to seed database");
    }

    pub async fn clean_db(&self) {
        // Clean up the database after tests
        // In a real implementation we would delete the DynamoDB table
        // For in-memory, nothing needs to be done as dropping the test context clears it
    }
}
