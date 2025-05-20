#[cfg(test)]
#[cfg(feature = "mssql")]
mod tests {
    use super::*;
    use crate::envelope::EnvelopeKeyRecord;
    use chrono::{Timelike, Utc};
    use std::collections::HashMap;
    use testcontainers::{clients, images::generic::GenericImage, RunnableImage};
    
    async fn create_test_envelope() -> EnvelopeKeyRecord {
        use crate::internal::{CryptoKey, KeyMeta, EncryptedKey};
        
        // Create a test key meta
        let key_meta = KeyMeta {
            id: "test_key".to_string(),
            created: Utc::now().with_nanosecond(0).unwrap(),
        };
        
        // Create an encrypted key
        let encrypted_key = EncryptedKey {
            key: vec![1, 2, 3, 4],
            created: Utc::now().with_nanosecond(0).unwrap(),
        };
        
        EnvelopeKeyRecord {
            parent_key_meta: None,
            encrypted_key,
            revoked: false,
        }
    }

    #[tokio::test]
    async fn test_mssql_metastore_basic_operations() {
        // Use testcontainers for SQL Server
        let docker = clients::Cli::default();
        
        // SQL Server 2019 image with environment variables
        let mssql_image = GenericImage::new("mcr.microsoft.com/mssql/server", "2019-latest")
            .with_env_var("ACCEPT_EULA", "Y")
            .with_env_var("SA_PASSWORD", "YourStrongPassword123!")
            .with_env_var("MSSQL_PID", "Express");
            
        let node = docker.run(mssql_image);
        let port = node.get_host_port_ipv4(1433);
        
        // Wait for SQL Server to be ready
        tokio::time::sleep(std::time::Duration::from_secs(15)).await;
        
        // Connect to SQL Server
        let connection_string = format!(
            "mssql://sa:YourStrongPassword123!@127.0.0.1:{}/master?TrustServerCertificate=true",
            port
        );
        let pool = MssqlPool::connect(&connection_string).await.unwrap();
        
        // Create test database
        sqlx::query("CREATE DATABASE TestDB")
            .execute(&pool)
            .await
            .unwrap();
        
        // Connect to test database
        let test_connection_string = format!(
            "mssql://sa:YourStrongPassword123!@127.0.0.1:{}/TestDB?TrustServerCertificate=true",
            port
        );
        let test_pool = MssqlPool::connect(&test_connection_string).await.unwrap();
        
        // Create encryption_key table
        sqlx::query(
            "CREATE TABLE encryption_key (
                id NVARCHAR(255) NOT NULL,
                created DATETIME2 NOT NULL,
                key_record NVARCHAR(MAX) NOT NULL,
                PRIMARY KEY (id, created)
            )"
        )
        .execute(&test_pool)
        .await
        .unwrap();
        
        // Create metastore
        let metastore = MssqlMetastore::new(Arc::new(test_pool));
        
        // Test store
        let envelope = create_test_envelope().await;
        let key_id = "test_key_1";
        let created = Utc::now().timestamp();
        
        let result = metastore.store(key_id, created, &envelope).await.unwrap();
        assert!(result);
        
        // Test load
        let loaded = metastore.load(key_id, created).await.unwrap();
        assert!(loaded.is_some());
        let loaded_envelope = loaded.unwrap();
        assert_eq!(loaded_envelope.encrypted_key.key, envelope.encrypted_key.key);
        assert_eq!(loaded_envelope.revoked, envelope.revoked);
        
        // Test duplicate store (should return false)
        let result = metastore.store(key_id, created, &envelope).await.unwrap();
        assert!(!result);
        
        // Test load_latest
        // Create another envelope with later timestamp
        let envelope2 = create_test_envelope().await;
        let created2 = created + 100;
        let result = metastore.store(key_id, created2, &envelope2).await.unwrap();
        assert!(result);
        
        let latest = metastore.load_latest(key_id).await.unwrap();
        assert!(latest.is_some());
        let latest_envelope = latest.unwrap();
        assert_eq!(latest_envelope.encrypted_key.key, envelope2.encrypted_key.key);
        
        // Test load non-existent key
        let missing = metastore.load("missing_key", created).await.unwrap();
        assert!(missing.is_none());
        
        // Test load_latest non-existent key
        let missing_latest = metastore.load_latest("missing_key").await.unwrap();
        assert!(missing_latest.is_none());
    }
    
    #[tokio::test]
    async fn test_mssql_metastore_error_handling() {
        // Create a pool that will fail to connect
        let bad_connection_string = "mssql://baduser:badpassword@localhost:1433/baddb";
        
        match MssqlPool::connect(bad_connection_string).await {
            Ok(_) => panic!("Expected connection to fail"),
            Err(e) => {
                println!("Expected error: {}", e);
            }
        }
    }
}