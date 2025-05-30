# SQL Metastore Implementations

This document describes the SQL metastore implementations available in the Asherah Rust port.

## Available SQL Metastores

The Rust port of Asherah provides the following SQL metastore implementations:

- `MySqlMetastore`: For MySQL databases
- `PostgresMetastore`: For PostgreSQL databases

## Required Database Table

Both implementations require the same database table structure:

```sql
CREATE TABLE encryption_key (
    id VARCHAR(255) NOT NULL,
    created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    key_record TEXT NOT NULL,
    PRIMARY KEY (id, created),
    INDEX (created)
);
```

For PostgreSQL, you may need to create the index separately:

```sql
CREATE INDEX idx_encryption_key_created ON encryption_key (created);
```

## Feature Flags

SQL metastore support is behind feature flags to avoid pulling in unnecessary dependencies. Add one of the following features to your `Cargo.toml`:

```toml
[dependencies]
appencryption = { version = "0.1.0", features = ["mysql"] }  # For MySQL support
appencryption = { version = "0.1.0", features = ["postgres"] }  # For PostgreSQL support
```

## Usage Examples

### MySQL Example

```rust
use appencryption::kms::StaticKeyManagementService;
use appencryption::metastore::MySqlMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::SessionFactory;
use securememory::protected_memory::DefaultSecretFactory;
use sqlx::mysql::MySqlPoolOptions;
use std::sync::Arc;

async fn example() -> Result<(), Box<dyn std::error::Error>> {
    // Create MySQL connection pool
    let pool = MySqlPoolOptions::new()
        .max_connections(5)
        .connect("mysql://user:password@localhost:3306/asherah")
        .await?;
    
    // Create MySQL metastore
    let metastore = Arc::new(MySqlMetastore::new(Arc::new(pool)));
    
    // Create other dependencies
    let policy = CryptoPolicy::new();
    let kms = Arc::new(StaticKeyManagementService::new(vec![0u8; 32]));
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory with MySQL metastore
    let factory = SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
    );
    
    // Use the factory...
    
    Ok(())
}
```

### PostgreSQL Example

```rust
use appencryption::kms::StaticKeyManagementService;
use appencryption::metastore::PostgresMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::SessionFactory;
use securememory::protected_memory::DefaultSecretFactory;
use sqlx::postgres::PgPoolOptions;
use std::sync::Arc;

async fn example() -> Result<(), Box<dyn std::error::Error>> {
    // Create PostgreSQL connection pool
    let pool = PgPoolOptions::new()
        .max_connections(5)
        .connect("postgres://user:password@localhost:5432/mydb")
        .await?;
    
    // Create PostgreSQL metastore
    let metastore = Arc::new(PostgresMetastore::new(Arc::new(pool)));
    
    // Create other dependencies
    let policy = CryptoPolicy::new();
    let kms = Arc::new(StaticKeyManagementService::new(vec![0u8; 32]));
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory with PostgreSQL metastore
    let factory = SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
    );
    
    // Use the factory...
    
    Ok(())
}
```

## Error Handling

Both implementations handle common SQL errors and map them to the appropriate Asherah errors:

- Duplicate key errors are handled gracefully and will return `false` from the `store` method
- Connection and query errors are converted to Asherah `Error::Metastore` errors with descriptive messages

## Performance Considerations

- Both implementations use connection pooling to efficiently manage database connections
- Metrics are recorded for all metastore operations using the standard Asherah metrics system
- You should adjust the connection pool size based on your application's concurrency needs