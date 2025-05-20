# Migration Guide: Moving to Rust Implementation

This guide helps users migrate from other language implementations of Asherah (Java, Go, C#) to the Rust implementation. It covers key differences, equivalent APIs, and recommendations for a smooth transition.

## Overview

The Rust implementation of Asherah provides all the same functionality as the other language implementations but with Rust's memory safety guarantees and performance characteristics. This guide will help you understand:

- API differences between implementations
- Feature equivalence and any Rust-specific features
- Recommended patterns for common operations
- Configuration mappings

## Core Concepts

All Asherah implementations share the same core concepts:

- **Envelope Encryption**: Data is encrypted with a Data Key, which is then encrypted with a Master Key.
- **Key Hierarchy**: System Keys → Intermediate Keys → Data Keys
- **Metastore**: Persistent storage for encrypted keys (e.g., DynamoDB, SQL databases)
- **KMS**: Key Management Service for the top-level keys (e.g., AWS KMS)
- **Session**: Interface for encrypting and decrypting data
- **Partition**: Logical grouping of keys (service/product/partition)

## API Comparison

### Java → Rust

| Java API | Rust API | Notes |
|----------|----------|-------|
| `SessionFactory.newBuilder()` | `SessionFactory::builder()` | Rust uses a more idiomatic builder pattern |
| `factory.buildSession(String partitionId)` | `factory.session(partition_id).await?` | Rust APIs are async with explicit error handling |
| `Session.encrypt(byte[] data)` | `session.encrypt(data).await?` | Similar API, but async in Rust |
| `Session.decrypt(byte[] data)` | `session.decrypt(data).await?` | Similar API, but async in Rust |
| `Session.close()` | `session.close().await?` | Similar API, but async in Rust |
| `CryptoPolicy.withSessionCacheMaxSize()` | `policy.with_session_cache_max_size()` | Similar API with Rust naming conventions |
| `CryptoPolicy.neverExpiring()` | `CryptoPolicy::never_expiring()` | Static constructor in Rust |

### Go → Rust

| Go API | Rust API | Notes |
|--------|----------|-------|
| `appencryption.NewSession()` | `session_factory.session().await?` | Rust uses factory pattern consistently |
| `session.Encrypt([]byte)` | `session.encrypt(data).await?` | Similar API with async/await in Rust |
| `session.Decrypt([]byte)` | `session.decrypt(data).await?` | Similar API with async/await in Rust |
| `session.Close()` | `session.close().await?` | Similar API with async/await in Rust |
| `appencryption.NewCryptoPolicy()` | `CryptoPolicy::new()` | Factory functions vs constructor |
| Go Options pattern | Rust Builder pattern | Rust uses builders rather than functional options |
| `metastore.Store(id, created, drr)` | `metastore.store(id, created, drr).await?` | Similar API with async/await in Rust |

### C# → Rust

| C# API | Rust API | Notes |
|--------|----------|-------|
| `SessionFactory.CreateSessionJsonImpl()` | `session_factory.get_session()` | Rust unifies byte and JSON handling |
| `Session.Encrypt(byte[])` | `session.encrypt()` | Similar API with async in Rust |
| `Session.Decrypt(byte[])` | `session.decrypt()` | Similar API with async in Rust |
| `new DefaultPartition()` | `Partition::new()` | Rust has unified partition handling |
| `Partition.GetIntermediateKeyId()` | Handled internally in Rust | More encapsulation in Rust |
| `new BasicExpiringCryptoPolicy()` | `CryptoPolicy::new()` | Simplified policy in Rust |
| `StaticKeyManagementService` | `StaticKeyManagementService` | Similar implementation |

## Feature Mapping

| Feature | Java | Go | C# | Rust | Notes |
|---------|------|----|----|------|-------|
| In-Memory Metastore | ✅ | ✅ | ✅ | ✅ | All implementations support this |
| DynamoDB Metastore | ✅ | ✅ | ✅ | ✅ | Rust has both AWS SDK v1 and v2 support |
| RDBMS Metastore | ✅ | ✅ | ✅ | ✅ | Rust supports MySQL, PostgreSQL, and SQL Server |
| AWS KMS | ✅ | ✅ | ✅ | ✅ | All implementations support AWS KMS |
| Static KMS | ✅ | ✅ | ✅ | ✅ | All implementations support static keys |
| Session Caching | ✅ | ✅ | ✅ | ✅ | All implementations support caching |
| Key Rotation | ✅ | ✅ | ✅ | ✅ | All implementations support key rotation |
| JSON Support | ✅ | ✅ | ✅ | ✅ | Rust unifies bytes and JSON handling |
| Metrics Integration | ✅ | ✅ | ✅ | ✅ | Rust uses the `metrics` crate |
| Async APIs | ❌ | Partial | Partial | ✅ | Rust is fully async/await |

## Migration Examples

### From Java to Rust

**Java code:**

```java
import com.godaddy.asherah.appencryption.*;
import com.godaddy.asherah.crypto.*;

// Create session factory
SessionFactory factory = SessionFactory.newBuilder()
    .withMetastore(new InMemoryMetastore())
    .withCryptoPolicy(new BasicExpiringCryptoPolicy())
    .withKms(new StaticKeyManagementService("masterKey"))
    .withPartition("service", "product")
    .build();

// Create session
try (Session<byte[], byte[]> session = factory.buildSession("user123")) {
    // Encrypt data
    byte[] data = "secret".getBytes();
    byte[] encrypted = session.encrypt(data);
    
    // Decrypt data
    byte[] decrypted = session.decrypt(encrypted);
}
```

**Equivalent Rust code:**

```rust
use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::InMemoryMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::SessionFactory;
use appencryption::Partition;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create dependencies
    let kms = Arc::new(StaticKeyManagementService::new("masterKey".to_string()));
    let metastore = Arc::new(InMemoryMetastore::new());
    
    // Create session factory with builder pattern
    let factory = SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(CryptoPolicy::new())
        .with_kms(kms)
        .with_metastore(metastore)
        .build()?;
    
    // Create session
    let session = factory.session("user123").await?;
    
    // Encrypt data
    let data = b"secret";
    let encrypted = session.encrypt(data).await?;
    
    // Decrypt data
    let decrypted = session.decrypt(&encrypted).await?;
    
    // Close the session
    session.close().await?;
    
    Ok(())
}
```

### From Go to Rust

**Go code:**

```go
import (
    "github.com/godaddy/asherah/go/appencryption"
    "github.com/godaddy/asherah/go/appencryption/pkg/kms"
    "github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

func main() {
    // Setup metastore
    metastore := persistence.NewMemoryMetastore()
    
    // Setup KMS
    kmsSvc := kms.NewStatic([]byte("masterKey"))
    
    // Create session
    sess, _ := appencryption.NewSession(
        "service",
        "product",
        "user123",
        appencryption.WithMetastore(metastore),
        appencryption.WithKMS(kmsSvc),
    )
    defer sess.Close()
    
    // Encrypt data
    data := []byte("secret")
    encrypted, _ := sess.Encrypt(data)
    
    // Decrypt data
    decrypted, _ := sess.Decrypt(encrypted)
}
```

**Equivalent Rust code:**

```rust
use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::InMemoryMetastore;
use appencryption::persistence::Persistence;
use appencryption::session::SessionFactory;
use appencryption::Partition;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Setup metastore and KMS
    let metastore = Arc::new(InMemoryMetastore::new());
    let kms = Arc::new(StaticKeyManagementService::new("masterKey".to_string()));
    
    // Create persistence layer
    let persistence = Persistence::new(metastore, kms);
    
    // Create partition
    let partition = Partition::new("service", "product");
    partition.with_user_id("user123");
    
    // Create session factory
    let factory = SessionFactory::new(persistence, None);
    
    // Get session
    let session = factory.get_session(&partition).await?;
    
    // Encrypt data
    let data = b"secret";
    let encrypted = session.encrypt(data).await?;
    
    // Decrypt data
    let decrypted = session.decrypt(&encrypted).await?;
    
    // Close the session
    session.close().await?;
    
    Ok(())
}
```

### From C# to Rust

**C# code:**

```csharp
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Persistence;

// Create dependencies
var metastore = new InMemoryMetastoreImpl();
var kms = new StaticKeyManagementServiceImpl("masterKey");
var policy = new BasicExpiringCryptoPolicy();

// Create session factory
var factory = new SessionFactory(
    metastore,
    kms, 
    policy,
    "service",
    "product");
    
// Create session
using (var session = factory.GetSessionBytes("user123"))
{
    // Encrypt data
    byte[] data = System.Text.Encoding.UTF8.GetBytes("secret");
    byte[] encrypted = session.Encrypt(data);
    
    // Decrypt data
    byte[] decrypted = session.Decrypt(encrypted);
}
```

**Equivalent Rust code:**

```rust
use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::InMemoryMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::SessionFactory;
use appencryption::Partition;
use std::sync::Arc;
use securememory::protected_memory::DefaultSecretFactory;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Create dependencies
    let metastore = Arc::new(InMemoryMetastore::new());
    let kms = Arc::new(StaticKeyManagementService::new("masterKey".to_string()));
    let policy = CryptoPolicy::new();
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory
    let factory = SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?;
    
    // Get session
    let session = factory.session("user123").await?;
    
    // Encrypt data
    let data = b"secret";
    let encrypted = session.encrypt(data).await?;
    
    // Decrypt data
    let decrypted = session.decrypt(&encrypted).await?;
    
    // Close the session
    session.close().await?;
    
    Ok(())
}
```

## Configuration Differences

### CryptoPolicy

| Old Implementation | Rust Implementation | Notes |
|-------------------|---------------------|-------|
| `setExpireAfter(Duration)` | `with_expire_after(Duration)` | Similar API |
| `setRevokeCheckInterval(Duration)` | `with_revoke_check_interval(Duration)` | Similar API |
| `withKeyExpirationDays(int)` | `with_key_expiration_days(i32)` | Similar API |
| `withRevokeCheckMinutes(int)` | `with_revoke_check_minutes(i32)` | Similar API |
| `withSessionCacheMaxSize(int)` | `with_session_cache_max_size(usize)` | Similar API |
| `withSessionCacheExpiry(Duration)` | `with_session_cache_duration(Duration)` | Similar API |
| `neverExpiring()` | `CryptoPolicy::never_expiring()` | Static constructor in Rust |

### Metastore

| Old Implementation | Rust Implementation | Notes |
|-------------------|---------------------|-------|
| `DynamoDbMetastoreImpl` | `DynamoDbMetastore` | Rust provides both AWS SDK v1 and v2 support |
| `JdbcMetastoreImpl` | `MysqlMetastore`, `PostgresMetastore`, `MssqlMetastore` | Rust has specific implementations for each database |
| `InMemoryMetastoreImpl` | `InMemoryMetastore` | Similar API |
| `SQLMetastore` | Not applicable | Rust has specific implementations for each database |

### KMS

| Old Implementation | Rust Implementation | Notes |
|-------------------|---------------------|-------|
| `AwsKeyManagementServiceImpl` | `AwsKms` | Rust provides both AWS SDK v1 and v2 support |
| `StaticKeyManagementServiceImpl` | `StaticKeyManagementService` | Similar API |

## Best Practices for Migration

1. **Start with a Small Component**: Begin migrating a small, isolated component to test the Rust implementation.

2. **Use the Async APIs Properly**: The Rust implementation uses async/await for all I/O operations. Ensure your application has a proper async runtime (typically Tokio).

3. **Leverage Rust's Type System**: Take advantage of Rust's strong type system for improved safety and expressiveness.

4. **Error Handling**: Use Rust's Result type for proper error handling. The Rust implementation returns detailed error types.

5. **Testing**: Write tests that verify the same encryption/decryption operations work in both implementations during migration.

6. **Shared Metastore**: You can point both the old and new implementations to the same metastore during migration to ensure key compatibility.

## Troubleshooting

### Common Issues

1. **Key Not Found**: If you get "Key not found" errors after migration, check that the partition naming convention is consistent between implementations.

2. **Incompatible Key Format**: If decryption fails, ensure you're using the same key format in both implementations. Some format differences might exist between versions.

3. **Performance Differences**: The Rust implementation might have different performance characteristics. Review caching settings if you notice significant differences.

4. **Memory Usage**: Rust's memory management is different, which may lead to different memory usage patterns. Monitor memory usage especially with large keys or datasets.

## Migration Checklist

- [ ] Review existing application's use of Asherah
- [ ] Map all configuration settings to Rust equivalents
- [ ] Set up Rust project with required dependencies
- [ ] Implement the Rust version alongside the existing implementation
- [ ] Test encryption/decryption compatibility between implementations
- [ ] Validate key storage and retrieval from metastore
- [ ] Benchmark performance to ensure comparable results
- [ ] Gradually transition traffic to the Rust implementation
- [ ] Monitor for errors and performance issues during transition

## Further Resources

- [Asherah Design and Architecture](../../docs/DesignAndArchitecture.md)
- [Rust Implementation Feature Matrix](./FEATURE_MATRIX.md)
- [SQL Metastore Documentation](./SQL_METASTORE.md)
- [AWS KMS Integration](../src/plugins/aws-v2/README.md)