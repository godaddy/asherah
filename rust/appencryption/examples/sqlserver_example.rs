//! Example demonstrating how to use the SQL Server metastore implementation
//!
//! This example shows how to set up and use the SQL Server metastore for key storage.
//!
//! NOTE: This example requires a SQL Server instance to be available.
//! You can use Docker to run a SQL Server instance locally:
//!
//! ```bash
//! docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Password123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest
//! ```

use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::MssqlMetastore;
use appencryption::persistence::Persistence;
use appencryption::session::{Session, SessionFactory};
use appencryption::Partition;
use log::{info, warn};
use std::error::Error;
use std::sync::Arc;

/// URL format for SQL Server connection string
const CONNECTION_STRING: &str =
    "server=localhost;database=asherah;user id=sa;password=Password123;TrustServerCertificate=true";

/// Example table name for key storage
const TABLE_NAME: &str = "encryption_key";

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error>> {
    // Initialize logger
    env_logger::init();

    info!("SQL Server Metastore Example");
    info!("===========================");

    info!("Connecting to SQL Server...");
    // In a real application, you would get this from configuration
    warn!("This example uses hardcoded credentials for demo purposes.");
    warn!("In a real application, use a secure configuration mechanism.");

    // Create the metastore
    // This will error if SQL Server is not available or the connection details are incorrect
    let metastore = match MssqlMetastore::new(CONNECTION_STRING).await {
        Ok(ms) => {
            info!("Successfully connected to SQL Server");
            ms
        }
        Err(e) => {
            warn!("Failed to connect to SQL Server: {}", e);
            warn!("This example requires a SQL Server instance to be available.");
            warn!("You can use Docker to run SQL Server locally:");
            warn!("docker run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=Password123\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest");
            return Err(Box::new(e));
        }
    };

    // Create the table if it doesn't exist
    info!("Creating table if it doesn't exist...");
    if let Err(e) = metastore.create_table_if_not_exists().await {
        warn!("Failed to create table: {}", e);
        return Err(Box::new(e));
    }
    info!("Table '{}' is ready", TABLE_NAME);

    // Create a KMS (using static KMS for simplicity)
    // In a real application, you would use a proper KMS like AWS KMS
    let master_key = "static-master-key-for-testing-only".to_string();
    let kms = Arc::new(StaticKeyManagementService::new(master_key));

    // Create persistence layer
    info!("Creating persistence layer with metastore and KMS...");
    let persistence = Persistence::new(Arc::new(metastore), kms);

    // Create a partition
    let service_name = "sqlserver-example";
    let product_id = "test-product";
    let partition = Partition::new(service_name, product_id);

    // Create session factory with default options
    info!("Creating session factory...");
    let session_factory = SessionFactory::new(persistence, None);

    // Get a session
    info!(
        "Getting a session for partition: service={}, product={}",
        service_name, product_id
    );
    let session = session_factory.get_session(&partition).await?;

    // Demonstrate encrypt/decrypt
    perform_encryption_operations(&session).await?;

    // Clean up
    session.close().await?;

    info!("Example completed successfully!");
    Ok(())
}

/// Performs encryption and decryption operations to demonstrate the metastore functionality
async fn perform_encryption_operations(session: &impl Session) -> Result<(), Box<dyn Error>> {
    // Example data to encrypt
    let data = b"This is sensitive data that should be protected with envelope encryption";

    info!("Encrypting data...");
    // Encrypt the data
    let encrypted = session.encrypt(data).await?;
    info!(
        "Data encrypted successfully. Encrypted length: {} bytes",
        encrypted.len()
    );

    // Display some details about the encrypted data
    info!("Encrypted data format: <DRR_BYTES>");
    info!("The encrypted data contains:");
    info!("  - Key metadata (ID, creation date)");
    info!("  - Encrypted data key");
    info!("  - Actual encrypted data");
    info!("  - Authentication tag");

    // Decrypt the data
    info!("Decrypting data...");
    let decrypted = session.decrypt(&encrypted).await?;
    info!(
        "Data decrypted successfully. Decrypted length: {} bytes",
        decrypted.len()
    );

    // Verify the decryption worked correctly
    assert_eq!(
        data.as_ref(),
        decrypted.as_slice(),
        "Decrypted data doesn't match original data"
    );
    info!("Verification successful: decrypted data matches the original");

    // Show how envelope encryption and the metastore work together
    info!("\nHow it works:");
    info!("1. When encrypting data:");
    info!("   - A unique data key is generated");
    info!("   - The data key is encrypted with the master key (KMS)");
    info!("   - The encrypted data key is stored in the metastore (SQL Server)");
    info!("   - The data is encrypted with the data key");
    info!("   - Metadata is packaged with the encrypted data");

    info!("2. When decrypting data:");
    info!("   - The metadata is extracted from the encrypted package");
    info!("   - The encrypted data key is retrieved from the metastore");
    info!("   - The data key is decrypted with the master key (KMS)");
    info!("   - The original data is decrypted with the data key");

    info!("\nSQL Server metastore stores and retrieves the encrypted keys");
    info!("This allows for secure key distribution across services and instances");

    Ok(())
}

/// Full implementation of SQL Server metastore (for reference)
///
/// Note: This is the same implementation as shown in the SQL_SERVER_METASTORE.md file.
/// The real implementation would be in the appencryption crate.
mod implementation_example {
    use appencryption::envelope::EnvelopeKeyRecord;
    use appencryption::error::{Error, Result};
    use appencryption::Metastore;
    use async_trait::async_trait;
    use sqlx::mssql::MssqlConnectOptions;
    use sqlx::{Executor, MssqlPool};
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

            self.pool
                .execute(query.as_str())
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
                    let record = serde_json::from_slice(&bytes).map_err(|e| {
                        Error::Metastore(format!("Failed to deserialize key record: {}", e))
                    })?;

                    Ok(Some(record))
                }
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
                    let record = serde_json::from_slice(&bytes).map_err(|e| {
                        Error::Metastore(format!("Failed to deserialize key record: {}", e))
                    })?;

                    Ok(Some(record))
                }
                None => Ok(None),
            }
        }

        async fn store(
            &self,
            id: &str,
            created: i64,
            envelope: &EnvelopeKeyRecord,
        ) -> Result<bool> {
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
}
