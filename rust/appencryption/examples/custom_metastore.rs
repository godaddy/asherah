use appencryption::{
    envelope::EnvelopeKeyRecord,
    kms::StaticKeyManagementService,
    metastore::{CompositeKey, KeyValueStore, TtlKeyValueStore},
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
    Error, Metastore, Result,
};
use async_trait::async_trait;
use securememory::protected_memory::DefaultSecretFactory;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::time::{SystemTime, UNIX_EPOCH};
use thiserror::Error;

/// Example demonstrating how to implement a custom Metastore for Asherah.
///
/// This example shows:
/// 1. How to create a custom Metastore implementation
/// 2. How to integrate the custom Metastore with Asherah
/// 3. Basic encrypt/decrypt operations using the custom Metastore
///
/// Note: This example shows how to directly implement the Metastore trait.
/// For a more modern approach using the generic KeyValueStore trait, see the
/// redis_kv_metastore.rs example.

#[derive(Error, Debug)]
enum RedisMetastoreError {
    #[error("Failed to acquire lock: {0}")]
    LockError(String),

    #[error("System time error: {0}")]
    TimeError(#[from] std::time::SystemTimeError),
}

// For compatibility with the KeyValueStore trait, we can implement this
// conversion between our custom error and the generic Error type
impl From<RedisMetastoreError> for Error {
    fn from(error: RedisMetastoreError) -> Self {
        Error::Metastore(error.to_string())
    }
}

/// A simple Redis-like Metastore implementation.
///
/// This is a simplified example that stores keys in memory with Redis-like TTL features.
/// In a real application, this would connect to a Redis server.
#[derive(Debug)]
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
    async fn expire(
        &self,
        id: &str,
        created: i64,
        ttl_seconds: i64,
    ) -> Result<bool, RedisMetastoreError> {
        let key = Self::generate_key(id, created);
        let mut store = self
            .store
            .write()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;

        if let Some(entry) = store.get_mut(&key) {
            // Calculate expiration time
            let now = SystemTime::now().duration_since(UNIX_EPOCH)?.as_secs() as i64;
            let expire_at = now + ttl_seconds;

            // Update the TTL
            entry.2 = Some(expire_at);
            return Ok(true);
        }

        Ok(false)
    }

    /// Delete a key
    async fn delete(&self, id: &str, created: i64) -> Result<bool, RedisMetastoreError> {
        let key = Self::generate_key(id, created);
        let mut store = self
            .store
            .write()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;
        Ok(store.remove(&key).is_some())
    }

    /// Check if a key is expired
    fn is_expired(&self, expire_at: Option<i64>) -> Result<bool, RedisMetastoreError> {
        if let Some(expire_time) = expire_at {
            let now = SystemTime::now().duration_since(UNIX_EPOCH)?.as_secs() as i64;
            return Ok(now >= expire_time);
        }
        Ok(false)
    }
}

// We can implement the KeyValueStore trait for better interoperability
#[async_trait]
impl KeyValueStore for RedisMetastore {
    type Key = String;
    type Value = EnvelopeKeyRecord;
    type Error = RedisMetastoreError;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
        let store = self
            .store
            .read()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;

        if let Some((record, _, expire_at)) = store.get(key) {
            // Check if the key is expired
            if self.is_expired(*expire_at)? {
                return Ok(None);
            }

            return Ok(Some(record.clone()));
        }

        Ok(None)
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool, Self::Error> {
        let mut store = self
            .store
            .write()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;

        // Check if key exists - we're using 0 as a placeholder for created since we don't
        // have the timestamp here (in a real implementation, we might parse it from the key)
        if only_if_absent && store.contains_key(key) {
            return Ok(false);
        }

        // Extract created timestamp (if we can parse it from the key)
        let created = 0; // Placeholder - in real implementation we'd extract this

        // Insert or update
        store.insert(key.clone(), (value.clone(), created, None));
        Ok(true)
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        let mut store = self
            .store
            .write()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;
        Ok(store.remove(key).is_some())
    }
}

// We can also implement the TtlKeyValueStore trait for TTL operations
#[async_trait]
impl TtlKeyValueStore for RedisMetastore {
    async fn expire(&self, key: &Self::Key, ttl_seconds: i64) -> Result<bool, Self::Error> {
        let mut store = self
            .store
            .write()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;

        if let Some(entry) = store.get_mut(key) {
            // Calculate expiration time
            let now = SystemTime::now().duration_since(UNIX_EPOCH)?.as_secs() as i64;
            let expire_at = now + ttl_seconds;

            // Update the TTL
            entry.2 = Some(expire_at);
            return Ok(true);
        }

        Ok(false)
    }

    async fn is_expired(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        let store = self
            .store
            .read()
            .map_err(|e| RedisMetastoreError::LockError(e.to_string()))?;

        match store.get(key) {
            Some((_, _, expire_at)) => self.is_expired(*expire_at),
            None => Ok(false), // Key doesn't exist, so it's not expired
        }
    }
}

#[async_trait]
impl Metastore for RedisMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let key = Self::generate_key(id, created);

        // We can now leverage our KeyValueStore implementation
        self.get(&key)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to load key: {}", e)))
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let store = self
            .store
            .read()
            .map_err(|e| Error::Metastore(format!("Failed to acquire read lock: {}", e)))?;

        // Find all keys for this id
        let mut latest_created = 0;
        let mut latest_record = None;

        for (key, (record, created, expire_at)) in store.iter() {
            if key.starts_with(&format!("{}_", id)) && *created > latest_created {
                // Skip expired keys
                if self
                    .is_expired(*expire_at)
                    .map_err(|e| Error::Metastore(format!("Error checking expiration: {}", e)))?
                {
                    continue;
                }

                latest_created = *created;
                latest_record = Some(record.clone());
            }
        }

        Ok(latest_record)
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let key = Self::generate_key(id, created);

        // We could just use our KeyValueStore implementation, but we need to handle
        // the created timestamp that isn't part of the KeyValueStore interface
        let mut store = self
            .store
            .write()
            .map_err(|e| Error::Metastore(format!("Failed to acquire write lock: {}", e)))?;

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

    let master_key = vec![0_u8; 32]; // In a real app, use a secure key
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
    let store = metastore
        .store
        .read()
        .map_err(|e| format!("Failed to acquire read lock: {}", e))?;
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
        metastore
            .expire(&system_key_id, created, 60)
            .await
            .map_err(|e| format!("Failed to set TTL: {}", e))?;

        // We could also delete keys:
        // metastore.delete(&system_key_id, created).await
        //    .map_err(|e| format!("Failed to delete key: {}", e))?;
    }

    // Close the sessions when done
    alice_session.close().await?;
    bob_session.close().await?;

    println!("Sessions closed. All operations successful!");

    Ok(())
}
