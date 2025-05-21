use appencryption::{
    kms::StaticKeyManagementService,
    metastore::{KeyValueStore, TtlKeyValueStore, StringKeyValueMetastore},
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
    Result,
};
#[cfg(feature = "async-trait-compat")]
use async_trait::async_trait;
use color_eyre::eyre::{self, WrapErr};
use env_logger;
use log::{debug, info, warn, error};
use securememory::protected_memory::DefaultSecretFactory;
use serde::{Serialize, Deserialize};
use std::collections::HashMap;
use std::fmt::Debug;
use std::sync::{Arc, RwLock};
use std::time::{SystemTime, UNIX_EPOCH};
use thiserror::Error;

/// Example demonstrating how to implement a custom key-value store and use it with Asherah.
///
/// This example shows:
/// 1. How to implement the KeyValueStore and TtlKeyValueStore traits
/// 2. How to use the StringKeyValueMetastore adapter to create a Metastore
/// 3. Basic encrypt/decrypt operations using the custom Metastore

// First, let's define our Redis-like storage error type
#[derive(Error, Debug)]
enum RedisError {
    #[error("Failed to acquire lock")]
    LockError(#[source] Box<dyn std::error::Error + Send + Sync>),
    
    #[error("System time error")]
    #[from]
    TimeError(#[source] std::time::SystemTimeError),
    
    #[error("Serialization error")]
    #[from]
    SerializationError(#[source] serde_json::Error),
}

// Define a simple data container to store with TTL
#[derive(Debug, Clone, Serialize, Deserialize)]
struct RedisEntry {
    value: String,
    expire_at: Option<i64>,
}

/// A simple Redis-like key-value store implementation.
///
/// This is a simplified in-memory implementation with Redis-like TTL features.
/// In a real application, this would connect to a Redis server.
#[derive(Debug)]
struct RedisStore {
    /// In-memory storage map for keys
    store: RwLock<HashMap<String, RedisEntry>>,
}

impl RedisStore {
    /// Create a new RedisStore
    fn new() -> Self {
        Self {
            store: RwLock::new(HashMap::new()),
        }
    }

    /// Check if a key is expired
    fn is_expired(&self, expire_at: Option<i64>) -> Result<bool, RedisError> {
        if let Some(expire_time) = expire_at {
            let now = SystemTime::now()
                .duration_since(UNIX_EPOCH)?
                .as_secs() as i64;
            return Ok(now >= expire_time);
        }
        Ok(false)
    }
    
    /// Get current time in seconds
    fn now() -> Result<i64, RedisError> {
        Ok(SystemTime::now()
            .duration_since(UNIX_EPOCH)?
            .as_secs() as i64)
    }
}

#[cfg_attr(feature = "async-trait-compat", async_trait)]
impl KeyValueStore for RedisStore {
    type Key = String;
    type Value = String;
    type Error = RedisError;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
        // Scope the lock to ensure it's dropped before any await points
        let result = {
            let store = self.store.read()
                .map_err(|e| RedisError::LockError(Box::new(e)))?;
                
            match store.get(key) {
                Some(entry) => {
                    // If the entry is present but expired, we return None
                    let expire_at = entry.expire_at;
                    if expire_at.is_some() {
                        Some((entry.value.clone(), expire_at))
                    } else {
                        // No expiration, return immediately
                        return Ok(Some(entry.value.clone()));
                    }
                }
                None => return Ok(None),
            }
        }; // Lock is dropped here
        
        // Now check expiration (if we have a value with expiration)
        if let Some((value, expire_at)) = result {
            if self.is_expired(expire_at)? {
                // Key is expired, consider it non-existent
                Ok(None)
            } else {
                Ok(Some(value))
            }
        } else {
            // This shouldn't happen with the early returns above,
            // but we include it for completeness
            Ok(None)
        }
    }

    async fn put(&self, key: &Self::Key, value: &Self::Value, only_if_absent: bool) -> Result<bool, Self::Error> {
        // Scope the lock to ensure it's dropped before any await points
        {
            let mut store = self.store.write()
                .map_err(|e| RedisError::LockError(Box::new(e)))?;
                
            // Check if key exists and is not expired
            let exists = match store.get(key) {
                Some(entry) => {
                    if let Some(expire_at) = entry.expire_at {
                        // Need to check expiration - do outside of lock
                        let now = Self::now()?;
                        now < expire_at
                    } else {
                        // No expiration, it exists
                        true
                    }
                }
                None => false,
            };
            
            if only_if_absent && exists {
                return Ok(false);
            }
            
            // Store the value with no expiration by default
            let entry = RedisEntry {
                value: value.clone(),
                expire_at: None,
            };
            
            store.insert(key.clone(), entry);
            return Ok(true);
        } // Lock is dropped here
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        let mut store = self.store.write()
            .map_err(|e| RedisError::LockError(Box::new(e)))?;
            
        Ok(store.remove(key).is_some())
    }
}

#[cfg_attr(feature = "async-trait-compat", async_trait)]
impl TtlKeyValueStore for RedisStore {
    async fn expire(&self, key: &Self::Key, ttl_seconds: i64) -> Result<bool, Self::Error> {
        // Calculate expiration time outside the lock
        let now = Self::now()?;
        let expire_at = now + ttl_seconds;
        
        // Scope the lock
        {
            let mut store = self.store.write()
                .map_err(|e| RedisError::LockError(Box::new(e)))?;
                
            if let Some(entry) = store.get_mut(key) {
                // Update the TTL
                entry.expire_at = Some(expire_at);
                return Ok(true);
            }
            
            Ok(false)
        } // Lock is dropped here
    }

    async fn is_expired(&self, key: &Self::Key) -> Result<bool, Self::Error> {
        // Get expire_at value from the store
        let expire_at = {
            let store = self.store.read()
                .map_err(|e| RedisError::LockError(Box::new(e)))?;
                
            match store.get(key) {
                Some(entry) => entry.expire_at,
                None => return Ok(false), // Non-existent keys are not expired
            }
        }; // Lock dropped here
        
        // Check expiration outside the lock
        self.is_expired(expire_at)
    }
}

#[tokio::main]
async fn main() -> color_eyre::Result<()> {
    // Set up better error handling with color-eyre
    color_eyre::install()?;
    
    // Initialize env_logger with default configuration
    env_logger::init();
    
    info!("Redis Key-Value Metastore Example");
    info!("=================================");

    // Create our custom Redis-like key-value store
    let redis_store = Arc::new(RedisStore::new());
    
    // Create a metastore using our key-value store and the adapter
    let metastore = Arc::new(StringKeyValueMetastore::new_string_store(redis_store.clone()));

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
        metastore,
        secret_factory,
        vec![],
    ));

    // Create sessions for different users
    let alice_session = factory.session("alice").await?;
    let bob_session = factory.session("bob").await?;

    // Encrypt data for Alice
    let alice_data = b"Alice's secret data".to_vec();
    debug!("Encrypting data for Alice");
    let alice_encrypted = alice_session.encrypt(&alice_data).await?;
    info!(
        "Encrypted Alice's data: {} bytes",
        alice_encrypted.data.len()
    );

    // Encrypt data for Bob
    let bob_data = b"Bob's confidential information".to_vec();
    debug!("Encrypting data for Bob");
    let bob_encrypted = bob_session.encrypt(&bob_data).await?;
    info!("Encrypted Bob's data: {} bytes", bob_encrypted.data.len());

    // Test the decrypt capability
    debug!("Decrypting data for Alice");
    let alice_decrypted = alice_session.decrypt(&alice_encrypted).await?;
    debug!("Decrypting data for Bob");
    let bob_decrypted = bob_session.decrypt(&bob_encrypted).await?;

    info!(
        "Alice's decrypted data: {}",
        String::from_utf8_lossy(&alice_decrypted)
    );
    info!(
        "Bob's decrypted data: {}",
        String::from_utf8_lossy(&bob_decrypted)
    );

    // Demonstration of Redis-like TTL feature
    // First, let's set a TTL on a custom key
    let ttl_key = "demo_ttl_key";
    let ttl_value = "This value will expire";
    
    info!("Demonstrating TTL feature");
    debug!("Storing value with key '{}'", ttl_key);
    
    // Store a value
    redis_store.put(&ttl_key.to_string(), &ttl_value.to_string(), false).await?;
    info!("Stored a value with key: {}", ttl_key);
    
    // Set a short TTL
    debug!("Setting TTL on key");
    redis_store.expire(&ttl_key.to_string(), 2).await?;
    info!("Set TTL of 2 seconds on the key");
    
    // Read it back before it expires
    debug!("Reading value before expiration");
    let value = redis_store.get(&ttl_key.to_string()).await?;
    info!("Value before expiration: {:?}", value);
    
    // Wait for it to expire
    info!("Waiting for key to expire...");
    tokio::time::sleep(tokio::time::Duration::from_secs(3)).await;
    
    // Try to read it after expiration
    debug!("Reading value after expiration");
    let value = redis_store.get(&ttl_key.to_string()).await?;
    info!("Value after expiration: {:?}", value);

    // Close the sessions when done
    debug!("Closing sessions");
    alice_session.close().await?;
    bob_session.close().await?;

    info!("Sessions closed. All operations successful!");

    Ok(())
}