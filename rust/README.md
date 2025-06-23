# Asherah Rust Implementation

This is the Rust implementation of Asherah, a library for application-level envelope encryption. It provides secure, high-performance, and flexible encryption tools with key caching, key rotation, and multiple backend integrations.

## Overview

Asherah Rust consists of two main crates:

1. **securememory**: Provides secure memory operations with platform-specific implementations for Unix and Windows.

2. **appencryption**: Implements application-level envelope encryption with a hierarchical key model.

## Documentation

For detailed information about the Rust implementation, please refer to these documents:

- [Feature Parity](./FEATURE_PARITY.md): Complete comparison of features between Go and Rust implementations
- [Performance Analysis](./PERFORMANCE.md): Benchmarks and performance characteristics
- [Port Status](./PORT_STATUS.md): Implementation status, details, and future directions

## Features

- **Secure memory management**: Lock, protect, and wipe sensitive data from memory
- **Envelope encryption**: Hierarchy of keys (System Keys, Intermediate Keys, Data Row Keys)
- **Key rotation and expiration**: Configurable policies for key management
- **Advanced caching mechanisms**:
  - Multiple eviction policies (LRU, LFU, TinyLFU)
  - Session caching with reference counting for efficient reuse
  - System key and intermediate key caching
  - Customizable cache sizes and TTLs
  - Eviction callbacks
- **Multiple backend support**:
  - KMS: AWS KMS integration with multi-region support
  - Metastore: In-memory, DynamoDB, and SQL implementations
- **Session-based API**: Simple interface for encryption/decryption operations
- **Persistence API**: Function adapters for custom persistence backends
- **Comprehensive logging**: Customizable logging interface

## Quick Start

### SecureMemory

```rust
use securememory::protected_memory::{ProtectedMemorySecret, DefaultSecretFactory};
use securememory::Secret;

fn main() {
    // Create a secret factory
    let factory = DefaultSecretFactory::new();
    
    // Create a secret
    let secret_data = b"sensitive information".to_vec();
    let secret = factory.create_secret(&secret_data).unwrap();
    
    // Use the secret
    secret.with_secret(|bytes| {
        assert_eq!(bytes, secret_data.as_slice());
    }).unwrap();
    
    // Secret will be automatically destroyed when it goes out of scope
}
```

### AppEncryption

```rust
use appencryption::policy::CryptoPolicy;
use appencryption::kms::StaticKeyManagementService;
use appencryption::metastore::MemoryMetastore;
use appencryption::session::{SessionFactory, Session};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

async fn example() -> Result<(), Box<dyn std::error::Error>> {
    // Create dependencies
    let mut policy = CryptoPolicy::new();
    
    // Enable caching for better performance
    policy.cache_system_keys = true;
    policy.cache_intermediate_keys = true;
    policy.cache_sessions = true;
    policy.session_cache_max_size = 10;
    policy.session_cache_duration = 3600; // 1 hour
    
    let master_key = vec![0u8; 32]; // In production, use a real master key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(MemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory
    let factory = SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
    );
    
    // Get session for a partition (creates a new one)
    let session1 = factory.session("user123").await?;
    
    // Encrypt data
    let data = b"secret data".to_vec();
    let encrypted = session1.encrypt(&data).await?;
    
    // Get another session for the same partition (reuses from cache)
    let session2 = factory.session("user123").await?;
    
    // Decrypt data with the second session
    let decrypted = session2.decrypt(&encrypted).await?;
    assert_eq!(data, decrypted);
    
    // Close sessions when done
    session1.close().await?;
    session2.close().await?;
    
    // Close factory when application shuts down
    factory.close().await?;
    
    Ok(())
}
```

## Using with Persistence

```rust
use appencryption::persistence::{LoaderFn, StorerFn};
use appencryption::envelope::DataRowRecord;
use std::sync::{Arc, Mutex};
use std::collections::HashMap;

// ... setup factory and session as in previous example

// Create a simple in-memory data store
let data_store = Arc::new(Mutex::new(HashMap::<String, DataRowRecord>::new()));
let record_key = "record_123".to_string();

// Create a StorerFn adapter
let store_fn = {
    let data_store = data_store.clone();
    let record_key = record_key.clone();
    
    StorerFn::new(move |drr: &DataRowRecord| {
        let mut store = data_store.lock().unwrap();
        store.insert(record_key.clone(), drr.clone());
        Ok(record_key.clone())
    })
};

// Create a LoaderFn adapter
let load_fn = {
    let data_store = data_store.clone();
    
    LoaderFn::new(move |key: &String| {
        let store = data_store.lock().unwrap();
        Ok(store.get(key).cloned())
    })
};

// Store and load data
let data = b"secret data".to_vec();
let stored_key = session.store(&data, store_fn).await?;
let loaded_data = session.load(&stored_key, load_fn).await?;
```

## Using Cache

```rust
use appencryption::cache::{CacheBuilder, CachePolicy};
use std::sync::Arc;
use std::time::Duration;

// Create an LRU cache
let lru_cache = CacheBuilder::<String, Vec<u8>>::new(100)
    .with_policy(CachePolicy::LRU)
    .with_ttl(Duration::from_secs(3600))
    .with_evict_callback(|key, value| {
        println!("Evicted key: {}, value size: {}", key, value.len());
    })
    .build();

// Put and get values
lru_cache.insert("key1".to_string(), vec![1, 2, 3]);
if let Some(value) = lru_cache.get(&"key1".to_string()) {
    println!("Got value: {:?}", value);
}

// Create an LFU cache
let lfu_cache = CacheBuilder::<String, Vec<u8>>::new(100)
    .with_policy(CachePolicy::LFU)
    .build();

// Create a TinyLFU cache (good for skewed access patterns)
let tlfu_cache = CacheBuilder::<String, Vec<u8>>::new(1000)
    .with_policy(CachePolicy::TLFU)
    .build();
```

## Using Logging

```rust
use appencryption::log::{set_logger, StdoutLogger, Logger, debug_enabled};

// Enable logging with the built-in stdout logger
set_logger(StdoutLogger::boxed());

// Check if logging is enabled
if debug_enabled() {
    println!("Debug logging is enabled");
}

// Create a custom logger
struct MyCustomLogger;

impl Logger for MyCustomLogger {
    fn debug(&self, message: &str) {
        println!("[MY_DEBUG] {}", message);
    }
    
    fn debugf(&self, fmt: std::fmt::Arguments) {
        println!("[MY_DEBUG] {}", fmt);
    }
}

// Use the custom logger
set_logger(Box::new(MyCustomLogger));

// Log messages
appencryption::log::debug("This is a debug message");
appencryption::debugf!("Formatted message: value = {}", 42);
```

## AWS Integration

### Basic AWS Integration

```rust
use appencryption::kms::aws_builder::AwsKmsClientBuilder;
use appencryption::metastore::{DynamoDbMetastore, StandardDynamoDbClient};
use std::sync::Arc;

// Create AWS KMS client
let kms = AwsKmsClientBuilder::new()
    .with_region("us-west-2")
    .with_key_id("alias/my-key")
    .build()
    .await?;

// Create DynamoDB client
let client = Arc::new(StandardDynamoDbClient::new("us-west-2").await?);

// Create DynamoDB metastore
let metastore = Arc::new(DynamoDbMetastore::new(
    client,
    Some("EncryptionKey".to_string()),
    false, // don't use region suffix
));

// Create session factory with AWS services
let factory = SessionFactory::new(
    "service",
    "product",
    CryptoPolicy::new(),
    Arc::new(kms),
    metastore,
    Arc::new(DefaultSecretFactory::new()),
);
```

### Multi-Region DynamoDB with Global Tables

```rust
use appencryption::metastore::{DynamoDbMetastore, DynamoDbClientBuilder};
use std::sync::Arc;
use std::time::Duration;

// Create a DynamoDB client with multi-region support
let (primary, replicas) = DynamoDbClientBuilder::new("us-west-2")
    .add_replica_region("us-east-1")
    .add_replica_region("eu-west-1")
    .with_timeout(Duration::from_secs(5))
    .build()
    .await?;

// Create a DynamoDB metastore with global table support
let metastore = Arc::new(DynamoDbMetastore::with_replicas(
    primary,
    replicas,
    Some("EncryptionKey".to_string()),
    true, // Use region suffix to prevent write conflicts
    true, // Prefer region-specific keys
));

// The metastore will now use the following client failover strategy:
// 1. Try the primary region first
// 2. If the primary region is unavailable, try replica regions
// 3. Automatically monitor health of all clients
// 4. Implement exponential backoff for retries
```

## Documentation

For more detailed documentation, please refer to the rustdoc documentation for each crate:

- `securememory`: Memory protection primitives
- `appencryption`: Application-level envelope encryption

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This library is licensed under the Apache License 2.0 - see the LICENSE file for details.