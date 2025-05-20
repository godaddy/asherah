use appencryption::{
    envelope::EnvelopeKeyRecord,
    kms::StaticKeyManagementService,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
    Metastore,
};
use async_trait::async_trait;
use securememory::protected_memory::DefaultSecretFactory;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::time::{SystemTime, UNIX_EPOCH};

/// Example demonstrating how to implement a custom Metastore for Asherah.
///
/// This example shows:
/// 1. How to create a custom Metastore implementation
/// 2. How to integrate the custom Metastore with Asherah
/// 3. Basic encrypt/decrypt operations using the custom Metastore

/// A simple Redis-like Metastore implementation.
///
/// This is a simplified example that stores keys in memory with Redis-like TTL features.
/// In a real application, this would connect to a Redis server.
struct RedisMetastore {
    /// In-memory storage map for keys
    store: RwLock<HashMap<String, (EnvelopeKeyRecord, i64, Option<i64>)>>,
}

impl RedisMetastore {
    /// Create a new RedisMetastore
    fn new() -> Self {
        Self {
            store: RwLock::new(HashMap::new()),
        }
    }

    /// Generate a key string from id and created timestamp
    fn generate_key(id: &str, created: i64) -> String {
        format!("{}_{}", id, created)
    }

    /// Set a TTL (time-to-live) for a key in seconds
    async fn expire(&self, id: &str, created: i64, ttl_seconds: i64) -> bool {
        let key = Self::generate_key(id, created);
        let mut store = self.store.write().unwrap();

        if let Some(entry) = store.get_mut(&key) {
            // Calculate expiration time
            let now = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_secs() as i64;
            let expire_at = now + ttl_seconds;

            // Update the TTL
            entry.2 = Some(expire_at);
            return true;
        }

        false
    }

    /// Delete a key
    async fn delete(&self, id: &str, created: i64) -> bool {
        let key = Self::generate_key(id, created);
        let mut store = self.store.write().unwrap();
        store.remove(&key).is_some()
    }

    /// Check if a key is expired
    fn is_expired(&self, expire_at: Option<i64>) -> bool {
        if let Some(expire_time) = expire_at {
            let now = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_secs() as i64;
            return now >= expire_time;
        }
        false
    }
}

#[async_trait]
impl Metastore for RedisMetastore {
    async fn load(
        &self,
        id: &str,
        created: i64,
    ) -> appencryption::Result<Option<EnvelopeKeyRecord>> {
        let key = Self::generate_key(id, created);
        let store = self.store.read().unwrap();

        if let Some((record, _, expire_at)) = store.get(&key) {
            // Check if the key is expired
            if self.is_expired(*expire_at) {
                return Ok(None);
            }

            return Ok(Some(record.clone()));
        }

        Ok(None)
    }

    async fn load_latest(&self, id: &str) -> appencryption::Result<Option<EnvelopeKeyRecord>> {
        let store = self.store.read().unwrap();

        // Find all keys for this id
        let mut latest_created = 0;
        let mut latest_record = None;

        for (key, (record, created, expire_at)) in store.iter() {
            if key.starts_with(&format!("{}_", id)) && *created > latest_created {
                // Skip expired keys
                if self.is_expired(*expire_at) {
                    continue;
                }

                latest_created = *created;
                latest_record = Some(record.clone());
            }
        }

        Ok(latest_record)
    }

    async fn store(
        &self,
        id: &str,
        created: i64,
        envelope: &EnvelopeKeyRecord,
    ) -> appencryption::Result<bool> {
        let key = Self::generate_key(id, created);
        let mut store = self.store.write().unwrap();

        // Only store if key doesn't exist
        if !store.contains_key(&key) {
            store.insert(key, (envelope.clone(), created, None));
            return Ok(true);
        }

        Ok(false)
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Custom Metastore Example");
    println!("=======================");

    // Create our custom Redis-like metastore
    let metastore = Arc::new(RedisMetastore::new());

    // Create other dependencies
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24);
    let cache_max_age = TimeDelta::hours(2);
    let create_date_precision = TimeDelta::minutes(1);

    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap());

    let master_key = vec![0u8; 32]; // In a real app, use a secure key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory with our custom metastore
    let factory = Arc::new(SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore.clone(),
        secret_factory,
        vec![],
    ));

    // Create sessions for different users
    let alice_session = factory.session("alice").await?;
    let bob_session = factory.session("bob").await?;

    // Encrypt data for Alice
    let alice_data = b"Alice's secret data".to_vec();
    let alice_encrypted = alice_session.encrypt(&alice_data).await?;
    println!(
        "Encrypted Alice's data: {} bytes",
        alice_encrypted.data.len()
    );

    // Encrypt data for Bob
    let bob_data = b"Bob's confidential information".to_vec();
    let bob_encrypted = bob_session.encrypt(&bob_data).await?;
    println!("Encrypted Bob's data: {} bytes", bob_encrypted.data.len());

    // Test the decrypt capability
    let alice_decrypted = alice_session.decrypt(&alice_encrypted).await?;
    let bob_decrypted = bob_session.decrypt(&bob_encrypted).await?;

    println!(
        "Alice's decrypted data: {}",
        String::from_utf8_lossy(&alice_decrypted)
    );
    println!(
        "Bob's decrypted data: {}",
        String::from_utf8_lossy(&bob_decrypted)
    );

    // Demonstration of Redis-like TTL feature
    // Get the system key ID from the session
    let system_key_id = format!("_SK_service_product");

    // Find a created timestamp from our store
    let store = metastore.store.read().unwrap();
    let mut some_key_created = None;

    for (key, (_, created, _)) in store.iter() {
        if key.starts_with(&format!("{}_", system_key_id)) {
            some_key_created = Some(*created);
            break;
        }
    }

    drop(store);

    // Set a TTL on one of the keys
    if let Some(created) = some_key_created {
        println!("Setting a TTL of 60 seconds on a system key...");
        metastore.expire(&system_key_id, created, 60).await;

        // We could also delete keys:
        // metastore.delete(&system_key_id, created).await;
    }

    // Close the sessions when done
    alice_session.close().await?;
    bob_session.close().await?;

    println!("Sessions closed. All operations successful!");

    Ok(())
}
