# Asherah - Application Encryption Library for Rust

This Rust implementation of Asherah provides application-level envelope encryption.

## Features

- **Envelope Encryption**: Implements multiple layers of encryption keys to securely protect sensitive data
- **Key Caching**: Efficient caching strategies for optimal performance 
- **Session Management**: Simple API for encryption sessions
- **Pluggable Storage**: Support for DynamoDB, MySQL, PostgreSQL, and in-memory storage
- **AWS KMS Integration**: Built-in support for AWS Key Management Service
- **Secure Memory Handling**: Protection of sensitive cryptographic material in memory
- **Cross-language Compatibility**: Compatible with Asherah implementations in other languages

## Usage

Add the dependency to your `Cargo.toml`:

```toml
[dependencies]
appencryption = { git = "https://github.com/godaddy/asherah" }
tokio = { version = "1", features = ["full"] }
```

## Basic Example

```rust
use appencryption::policy::CryptoPolicy;
use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::memory::InMemoryMetastore;
use appencryption::{LoaderFn, StorerFn, SessionFactory};
use securememory::memguard::SecretFactory;

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create a KMS (using static key for demo)
    let kms = Arc::new(StaticKeyManagementService::new(&[0; 32])?);
    
    // Create a metastore (using in-memory for demo)
    let metastore = Arc::new(InMemoryMetastore::new());
    
    // Create a default crypto policy
    let policy = CryptoPolicy::default();
    
    // Create the session factory
    let factory = SessionFactory::builder()
        .with_service("my-service")
        .with_product("my-product")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(Arc::new(SecretFactory))
        .build()?;
    
    // Create a session for a partition
    let session = factory.session("user123").await?;
    
    // Create a simple in-memory data store
    let data_store = Arc::new(Mutex::new(HashMap::new()));
    
    // Create store/load functions
    let store_fn = {
        let data_store = data_store.clone();
        StorerFn::new(move |drr| {
            let key = "record1".to_string();
            let mut store = data_store.lock().unwrap();
            store.insert(key.clone(), drr.clone());
            Ok(key)
        })
    };
    
    let load_fn = {
        let data_store = data_store.clone();
        LoaderFn::new(move |key: &String| {
            let store = data_store.lock().unwrap();
            Ok(store.get(key).cloned())
        })
    };
    
    // Encrypt and store data
    let data = b"secret data".to_vec();
    let key = session.store(&data, store_fn).await?;
    
    // Load and decrypt data
    let decrypted = session.load(&key, load_fn).await?;
    assert_eq!(data, decrypted);
    
    // Close resources
    session.close().await?;
    factory.close().await?;
    
    Ok(())
}
```

## Advanced Features

### SessionFactory Builder Pattern

The `SessionFactory` provides a builder pattern for better ergonomics:

```rust
let factory = SessionFactory::builder()
    .with_service("my-service")
    .with_product("my-product")
    .with_policy(policy)
    .with_kms(kms)
    .with_metastore(metastore)
    .with_secret_factory(secret_factory)
    .with_metrics(true)  // Enable metrics
    .build()?;
```

### Advanced Caching Configuration

The `CryptoPolicy` allows detailed configuration of caching strategies:

```rust
let mut policy = CryptoPolicy::default();

// Configure caching strategies
policy.cache_system_keys = true;
policy.cache_intermediate_keys = true;
policy.shared_intermediate_key_cache = true;
policy.cache_sessions = true;

// Set cache durations and limits
policy.session_cache_duration = 300; // 5 minutes
policy.session_cache_max_size = 1000;
policy.system_key_cache_max_size = 1000;
policy.intermediate_key_cache_max_size = 1000;

// Set cache eviction policies
policy.session_cache_eviction_policy = "lru".to_string(); // LRU eviction
policy.system_key_cache_eviction_policy = "lru".to_string();
policy.intermediate_key_cache_eviction_policy = "lfu".to_string(); // LFU for intermediate keys
```

### Region-Specific Partitioning

For multi-region deployments, you can create sessions with region suffixes:

```rust
// Create a session with a region suffix
let suffix_session = factory.session_with_suffix("user123", "us-west-2").await?;
```

### Factory Options

You can provide custom configuration options:

```rust
let factory = SessionFactory::builder()
    // ... other configuration ...
    .with_option(|f| {
        // Custom factory option
        println!("Customizing factory: {:?}", f.policy);
    })
    .build()?;
```

## Example Applications

See the `examples` directory for sample applications:

- `basic_usage.rs`: Simple encryption/decryption example
- `advanced_session_factory.rs`: Demonstrates advanced caching and configuration
- `mysql_example.rs`: Using MySQL as a metastore
- `dynamodb_global.rs`: Global table configuration with DynamoDB

## License

MIT