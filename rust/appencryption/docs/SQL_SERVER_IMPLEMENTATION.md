# Adding SQL Server Support to AppEncryption

## 1. Update Cargo.toml

Add SQL Server support to the features and dependencies:

```toml
[dependencies]
# ... existing dependencies ...
sqlx = { version = "0.7", features = ["runtime-tokio-rustls", "mysql", "postgres", "mssql", "chrono", "json"], optional = true }

[features]
# ... existing features ...
sql = ["sqlx"]
mysql = ["sql", "sqlx/mysql"]
postgres = ["sql", "sqlx/postgres"]
mssql = ["sql", "sqlx/mssql"]  # New SQL Server feature
```

## 2. Create SQL Server Metastore Implementation

Create a new file `src/metastore/mssql.rs`:

```rust
use crate::envelope::EnvelopeKeyRecord;
use crate::error::{Error, Result};
use crate::Metastore;

use async_trait::async_trait;
use chrono::{DateTime, TimeZone, Utc};
use metrics::timer;
use sqlx::{Mssql, MssqlPool};
use std::sync::Arc;
use std::time::Instant;

// SQL Server uses @p1, @p2, etc. for parameters
const LOAD_KEY_QUERY: &str = "SELECT key_record FROM encryption_key WHERE id = @p1 AND created = @p2";
const STORE_KEY_QUERY: &str = "INSERT INTO encryption_key (id, created, key_record) VALUES (@p1, @p2, @p3)";
const LOAD_LATEST_QUERY: &str = "SELECT TOP 1 key_record FROM encryption_key WHERE id = @p1 ORDER BY created DESC";

/// SQL Server metastore implementation
pub struct MssqlMetastore {
    /// Connection pool for SQL Server
    pool: Arc<MssqlPool>,
}

impl MssqlMetastore {
    /// Creates a new SQL Server metastore with the given connection pool
    pub fn new(pool: Arc<MssqlPool>) -> Self {
        Self { pool }
    }

    /// Parses an envelope key record from a JSON string
    fn parse_envelope(json_str: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json_str)
            .map_err(|e| Error::Metastore(format!("Unable to parse key: {}", e)))
    }
}

#[async_trait]
impl Metastore for MssqlMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let timer = metrics::timer!("ael.metastore.mssql.load");
        let start = Instant::now();
        
        let created_dt = Utc.timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;
            
        let result = sqlx::query_as::<Mssql, (String,)>(LOAD_KEY_QUERY)
            .bind(id)
            .bind(created_dt)
            .fetch_optional(&*self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Error loading key: {}", e)))?;
            
        timer.stop();
        
        match result {
            Some((json_str,)) => {
                let envelope = Self::parse_envelope(&json_str)?;
                Ok(Some(envelope))
            }
            None => Ok(None),
        }
    }
    
    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let timer = metrics::timer!("ael.metastore.mssql.loadlatest");
        let start = Instant::now();
        
        let result = sqlx::query_as::<Mssql, (String,)>(LOAD_LATEST_QUERY)
            .bind(id)
            .fetch_optional(&*self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Error loading latest key: {}", e)))?;
            
        timer.stop();
        
        match result {
            Some((json_str,)) => {
                let envelope = Self::parse_envelope(&json_str)?;
                Ok(Some(envelope))
            }
            None => Ok(None),
        }
    }
    
    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let timer = metrics::timer!("ael.metastore.mssql.store");
        let start = Instant::now();
        
        let created_dt = Utc.timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;
            
        let json_str = serde_json::to_string(envelope)
            .map_err(|e| Error::Metastore(format!("Failed to serialize key: {}", e)))?;
            
        let result = sqlx::query(STORE_KEY_QUERY)
            .bind(id)
            .bind(created_dt)
            .bind(json_str)
            .execute(&*self.pool)
            .await;
            
        timer.stop();
        
        match result {
            Ok(query_result) => Ok(query_result.rows_affected() > 0),
            Err(e) => {
                // Handle duplicate key errors gracefully
                if e.to_string().contains("duplicate") || e.to_string().contains("UNIQUE") {
                    Ok(false)
                } else {
                    Err(Error::Metastore(format!("Error storing key: {}", e)))
                }
            }
        }
    }
}
```

## 3. Update Module Exports

Update `src/metastore/mod.rs`:

```rust
// ... existing imports ...

#[cfg(feature = "mssql")]
mod mssql;

// ... existing exports ...

#[cfg(feature = "mssql")]
pub use mssql::MssqlMetastore;
```

## 4. Update SQL Module

Update `src/persistence/sql.rs` to include SQL Server support:

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SqlMetastoreDbType {
    /// MySQL database
    MySql,
    
    /// PostgreSQL database
    Postgres,
    
    /// Oracle database
    Oracle,
    
    /// SQL Server database
    SqlServer,
}

impl SqlMetastoreDbType {
    /// Converts SQL placeholders to the database-specific format
    fn convert_placeholders(&self, sql: &str) -> String {
        match self {
            SqlMetastoreDbType::MySql => sql.to_string(),
            SqlMetastoreDbType::Postgres => {
                // Convert ? to $1, $2, etc.
                let re = Regex::new(r"\?").unwrap();
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures| {
                    counter += 1;
                    format!("${}", counter)
                }).to_string()
            }
            SqlMetastoreDbType::Oracle => {
                // Convert ? to :1, :2, etc.
                let re = Regex::new(r"\?").unwrap();
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures| {
                    counter += 1;
                    format!(":{}", counter)
                }).to_string()
            }
            SqlMetastoreDbType::SqlServer => {
                // Convert ? to @p1, @p2, etc.
                let re = Regex::new(r"\?").unwrap();
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures| {
                    counter += 1;
                    format!("@p{}", counter)
                }).to_string()
            }
        }
    }
}
```

## 5. Create SQL Server Schema

The SQL Server table schema should be:

```sql
CREATE TABLE encryption_key (
    id NVARCHAR(255) NOT NULL,
    created DATETIME2 NOT NULL,
    key_record NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY (id, created)
);

-- Optional: Add index for better performance on load_latest queries
CREATE INDEX idx_encryption_key_id_created ON encryption_key(id, created DESC);
```

## 6. Connection String Format

SQL Server connection strings for sqlx follow this format:

```
mssql://username:password@host:port/database
```

Example:
```
mssql://sa:YourPassword@localhost:1433/TestDB
```

## 7. Testing

Create a test file `src/metastore/mssql_test.rs`:

```rust
#[cfg(test)]
#[cfg(feature = "mssql")]
mod tests {
    use super::*;
    use testcontainers::{clients, images::generic::GenericImage};
    
    #[tokio::test]
    async fn test_mssql_metastore() {
        // Use testcontainers for SQL Server
        let docker = clients::Cli::default();
        let mssql_image = GenericImage::new("mcr.microsoft.com/mssql/server", "2019-latest")
            .with_env_var("ACCEPT_EULA", "Y")
            .with_env_var("SA_PASSWORD", "YourStrongPassword123!");
            
        let node = docker.run(mssql_image);
        let port = node.get_host_port_ipv4(1433);
        
        // Wait for SQL Server to be ready
        tokio::time::sleep(std::time::Duration::from_secs(10)).await;
        
        // Connect and create schema
        let connection_string = format!("mssql://sa:YourStrongPassword123!@127.0.0.1:{}/master", port);
        let pool = MssqlPool::connect(&connection_string).await.unwrap();
        
        // Create test database and table
        sqlx::query("CREATE DATABASE TestDB").execute(&pool).await.unwrap();
        sqlx::query("USE TestDB").execute(&pool).await.unwrap();
        sqlx::query(
            "CREATE TABLE encryption_key (
                id NVARCHAR(255) NOT NULL,
                created DATETIME2 NOT NULL,
                key_record NVARCHAR(MAX) NOT NULL,
                PRIMARY KEY (id, created)
            )"
        ).execute(&pool).await.unwrap();
        
        // Test metastore operations
        let metastore = MssqlMetastore::new(Arc::new(pool));
        
        // Test store and load
        let envelope = create_test_envelope();
        let result = metastore.store("test_key", 12345, &envelope).await.unwrap();
        assert!(result);
        
        let loaded = metastore.load("test_key", 12345).await.unwrap();
        assert!(loaded.is_some());
        assert_eq!(loaded.unwrap(), envelope);
    }
}
```

## 8. Update Documentation

Add SQL Server to the documentation:

```rust
//! - SQL Server metastore for Microsoft SQL Server integration (requires the 'mssql' feature)
```

## Usage Example

```rust
use sqlx::MssqlPool;
use appencryption::metastore::MssqlMetastore;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Connect to SQL Server
    let pool = MssqlPool::connect("mssql://sa:password@localhost:1433/MyDatabase").await?;
    
    // Create metastore
    let metastore = MssqlMetastore::new(Arc::new(pool));
    
    // Use with SessionFactory
    let factory = SessionFactory::builder()
        .with_metastore(Arc::new(metastore))
        .build()?;
    
    Ok(())
}
```

## Notes

1. SQL Server uses `TOP` instead of `LIMIT` for query limiting
2. Parameter placeholders are `@p1`, `@p2`, etc.
3. SQL Server uses `DATETIME2` for timestamp storage
4. Connection requires the `mssql` feature to be enabled
5. Make sure to handle SQL Server-specific error messages for duplicate keys