# SQL Server Metastore Implementation

This document describes how to implement and use the SQL Server metastore for Asherah's envelope encryption system.

## Overview

The SQL Server metastore allows you to store encryption keys in a Microsoft SQL Server database. This provides durability and scalability for your encryption keys, especially in scenarios where you need:

- Centralized key management across multiple applications or services
- High availability and disaster recovery features provided by SQL Server
- Enterprise-grade backup and recovery mechanisms
- Fine-grained access controls via SQL Server security features

## Schema Setup

Before using the SQL Server metastore, you'll need to create the required database schema. Use the following SQL script to create the encryption_key table:

```sql
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[encryption_key]') AND type in (N'U'))
BEGIN
  CREATE TABLE [dbo].[encryption_key] (
    [id] NVARCHAR(255) NOT NULL,
    [created] BIGINT NOT NULL,
    [key_record] VARBINARY(MAX) NOT NULL,
    CONSTRAINT [PK_encryption_key] PRIMARY KEY CLUSTERED ([id], [created])
  )
END
```

Key components of the schema:
- `id`: The key ID, typically a combination of service and product IDs
- `created`: Timestamp (in milliseconds since epoch) when the key was created
- `key_record`: Binary blob containing the encrypted key data

## Usage

To use the SQL Server metastore in your application, follow these steps:

### 1. Add the Required Dependencies

Add the SQL Server driver and the SQL metastore feature to your `Cargo.toml`:

```toml
[dependencies]
appencryption = { version = "0.1.0", features = ["mssql"] }
sqlx = { version = "0.7", features = ["runtime-tokio-rustls", "mssql"] }
```

### 2. Create the Metastore

Initialize the SQL Server metastore with your connection string:

```rust
use appencryption::metastore::MssqlMetastore;
use std::sync::Arc;

async fn create_metastore() -> Arc<MssqlMetastore> {
    let connection_string = "server=localhost;database=asherah;user id=sa;password=Password123;TrustServerCertificate=true";
    
    // Create and initialize the metastore
    let metastore = MssqlMetastore::new(connection_string).await
        .expect("Failed to connect to SQL Server");
        
    // Create the table if it doesn't exist
    metastore.create_table_if_not_exists().await
        .expect("Failed to create encryption_key table");
        
    Arc::new(metastore)
}
```

### 3. Use with SessionFactory

Once you have your metastore, integrate it with the SessionFactory:

```rust
use appencryption::kms::StaticKeyManagementService;
use appencryption::persistence::Persistence;
use appencryption::session::SessionFactory;
use appencryption::Partition;
use std::sync::Arc;

async fn setup_session_factory(metastore: Arc<dyn Metastore>) -> SessionFactory {
    // Create a KMS (using static KMS for simplicity)
    let kms = Arc::new(StaticKeyManagementService::new("some-master-key".to_string()));
    
    // Create persistence with the metastore and KMS
    let persistence = Persistence::new(metastore, kms);
    
    // Create session factory
    SessionFactory::new(persistence, None)
}

// Usage example
async fn encrypt_data() {
    let metastore = create_metastore().await;
    let session_factory = setup_session_factory(metastore).await;
    
    // Create a partition
    let partition = Partition::new("service", "product");
    
    // Get a session
    let session = session_factory.get_session(&partition).await
        .expect("Failed to create session");
        
    // Encrypt data
    let data = b"sensitive data";
    let encrypted = session.encrypt(data).await
        .expect("Failed to encrypt data");
        
    // Data can now be decrypted by any service with access to the same metastore
    let decrypted = session.decrypt(&encrypted).await
        .expect("Failed to decrypt data");
        
    assert_eq!(data.as_ref(), decrypted.as_slice());
}
```

## Implementation Details

The SQL Server metastore implementation uses the following techniques:

1. **Connection Pooling**: The metastore maintains a pool of database connections to minimize connection overhead.

2. **Parameterized Queries**: All SQL queries use parameterized statements to prevent SQL injection.

3. **Binary Serialization**: Key records are stored as binary data to ensure efficient storage and retrieval.

4. **Optimistic Concurrency**: The implementation uses SQL Server's transaction support for safe concurrent access.

## Performance Considerations

When using the SQL Server metastore, consider these performance factors:

1. **Connection String Options**: Include appropriate connection string options:
   - `TrustServerCertificate=true` for development (use proper certificates in production)
   - `Max Pool Size` to control connection pooling
   - `Connect Timeout` for controlling connection timeouts

2. **Indexing**: The default schema creates a primary key on (id, created), which provides efficient lookups. For high-volume workloads, consider additional indexes.

3. **Caching**: Use Asherah's built-in caching capabilities to reduce database load.

## Security Best Practices

1. **Least Privilege Accounts**: Use a database account with minimal necessary privileges.

2. **Connection Encryption**: Enable TLS/SSL for all database connections.

3. **Always Encrypt**: For additional security in SQL Server Enterprise, consider using the Always Encrypt feature.

4. **Audit Logging**: Enable SQL Server audit logging for key operations to maintain an audit trail.

## Example: Implementing a Complete SQL Server Metastore

Below is an example of how a full SQL Server metastore implementation might look:

```rust
use crate::envelope::EnvelopeKeyRecord;
use crate::error::{Error, Result};
use crate::Metastore;
use async_trait::async_trait;
use sqlx::{Executor, MssqlPool, query, query_as};
use sqlx::mssql::MssqlConnectOptions;
use std::sync::Arc;
use std::str::FromStr;

/// SQL Server metastore implementation 
pub struct MssqlMetastore {
    pool: MssqlPool,
    table_name: String,
}

impl MssqlMetastore {
    /// Creates a new SQL Server metastore with the given connection string
    pub async fn new(connection_string: &str) -> Result<Self> {
        let options = MssqlConnectOptions::from_str(connection_string)
            .map_err(|e| Error::Metastore(format!("Invalid connection string: {}", e)))?;
            
        let pool = MssqlPool::connect_with(options)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to connect to SQL Server: {}", e)))?;
            
        Ok(Self {
            pool,
            table_name: "encryption_key".to_string(),
        })
    }
    
    /// Creates a new SQL Server metastore with the given connection string and table name
    pub async fn with_table_name(connection_string: &str, table_name: &str) -> Result<Self> {
        let options = MssqlConnectOptions::from_str(connection_string)
            .map_err(|e| Error::Metastore(format!("Invalid connection string: {}", e)))?;
            
        let pool = MssqlPool::connect_with(options)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to connect to SQL Server: {}", e)))?;
            
        Ok(Self {
            pool,
            table_name: table_name.to_string(),
        })
    }
    
    /// Creates the encryption_key table if it doesn't exist
    pub async fn create_table_if_not_exists(&self) -> Result<()> {
        let query = format!(
            r#"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{}]') AND type in (N'U'))
            BEGIN
              CREATE TABLE [dbo].[{}] (
                [id] NVARCHAR(255) NOT NULL,
                [created] BIGINT NOT NULL,
                [key_record] VARBINARY(MAX) NOT NULL,
                CONSTRAINT [PK_{}] PRIMARY KEY CLUSTERED ([id], [created])
              )
            END
            "#, 
            self.table_name, self.table_name, self.table_name
        );
        
        self.pool.execute(query.as_str())
            .await
            .map_err(|e| Error::Metastore(format!("Failed to create table: {}", e)))?;
            
        Ok(())
    }
}

#[async_trait]
impl Metastore for MssqlMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let query = format!(
            "SELECT key_record FROM [{}] WHERE id = @p1 AND created = @p2",
            self.table_name
        );
        
        let result = sqlx::query(&query)
            .bind(id)
            .bind(created)
            .map(|row: sqlx::mssql::MssqlRow| {
                let bytes: Vec<u8> = row.get("key_record");
                bytes
            })
            .fetch_optional(&self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to load key: {}", e)))?;
            
        match result {
            Some(bytes) => {
                // Deserialize the record
                let record = serde_json::from_slice(&bytes)
                    .map_err(|e| Error::Metastore(format!("Failed to deserialize key record: {}", e)))?;
                    
                Ok(Some(record))
            },
            None => Ok(None),
        }
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let query = format!(
            "SELECT TOP 1 key_record FROM [{}] WHERE id = @p1 ORDER BY created DESC",
            self.table_name
        );
        
        let result = sqlx::query(&query)
            .bind(id)
            .map(|row: sqlx::mssql::MssqlRow| {
                let bytes: Vec<u8> = row.get("key_record");
                bytes
            })
            .fetch_optional(&self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to load latest key: {}", e)))?;
            
        match result {
            Some(bytes) => {
                // Deserialize the record
                let record = serde_json::from_slice(&bytes)
                    .map_err(|e| Error::Metastore(format!("Failed to deserialize key record: {}", e)))?;
                    
                Ok(Some(record))
            },
            None => Ok(None),
        }
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        // Serialize the envelope to JSON
        let bytes = serde_json::to_vec(envelope)
            .map_err(|e| Error::Metastore(format!("Failed to serialize key record: {}", e)))?;
            
        let query = format!(
            "INSERT INTO [{}] (id, created, key_record) VALUES (@p1, @p2, @p3)",
            self.table_name
        );
        
        let result = sqlx::query(&query)
            .bind(id)
            .bind(created)
            .bind(&bytes)
            .execute(&self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Failed to store key: {}", e)))?;
            
        Ok(result.rows_affected() > 0)
    }
}
```

## Troubleshooting

Common issues and solutions:

1. **Connection Failures**:
   - Verify the connection string is correct
   - Ensure SQL Server is running and accessible
   - Check network connectivity and firewall settings

2. **Permission Errors**:
   - Ensure the database user has the necessary permissions
   - Required permissions include: CREATE TABLE, SELECT, INSERT

3. **Performance Issues**:
   - Monitor SQL Server query performance
   - Consider adding indexes for frequent query patterns
   - Review the connection pooling configuration

## References

- [SQL Server Documentation](https://docs.microsoft.com/en-us/sql/sql-server/)
- [sqlx Crate Documentation](https://docs.rs/sqlx/)
- [Asherah Architecture Documentation](../../docs/DesignAndArchitecture.md)