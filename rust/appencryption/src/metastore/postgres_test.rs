#[cfg(test)]
mod tests {
    use super::*;
    use chrono::Utc;
    use crate::envelope::EnvelopeKeyRecord;
    use crate::Metastore;
    use sqlx::{Postgres, PgPool};
    use std::sync::Arc;
    use testcontainers::{clients, images::postgres::Postgres as PostgresImage, Docker};

    // Setup test helpers
    async fn setup_test_db() -> (Arc<PgPool>, clients::Cli) {
        let docker = clients::Cli::default();
        let postgres_container = docker.run(PostgresImage::default());
        let port = postgres_container.get_host_port_ipv4(5432);
        let url = format!(
            "postgres://postgres:postgres@localhost:{}/postgres",
            port
        );

        // Create connection pool
        let pool = Arc::new(
            PgPool::connect(&url)
                .await
                .expect("Failed to connect to PostgreSQL")
        );

        // Create table if it doesn't exist
        sqlx::query(
            "CREATE TABLE IF NOT EXISTS encryption_key (
                id VARCHAR(255) NOT NULL,
                created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                key_record TEXT NOT NULL,
                PRIMARY KEY (id, created)
            )"
        )
        .execute(&*pool)
        .await
        .expect("Failed to create table");

        // Create index on created column
        sqlx::query(
            "CREATE INDEX IF NOT EXISTS idx_encryption_key_created ON encryption_key (created)"
        )
        .execute(&*pool)
        .await
        .expect("Failed to create index");

        (pool, docker)
    }

    #[tokio::test]
    async fn test_postgres_metastore_store_and_load() {
        let (pool, _docker) = setup_test_db().await;
        let metastore = PostgresMetastore::new(pool);

        // Create test data
        let id = "test_key_id";
        let created = Utc::now().timestamp();
        let envelope = EnvelopeKeyRecord {
            created: created,
            key: "encrypted_key_data".to_string(),
            parent_key_meta: None,
            revoked: false,
        };

        // Store key
        let store_result = metastore.store(id, created, &envelope).await;
        assert!(store_result.is_ok());
        assert!(store_result.unwrap());

        // Load key
        let load_result = metastore.load(id, created).await;
        assert!(load_result.is_ok());

        let loaded_envelope = load_result.unwrap();
        assert!(loaded_envelope.is_some());

        let loaded_envelope = loaded_envelope.unwrap();
        assert_eq!(loaded_envelope.created, envelope.created);
        assert_eq!(loaded_envelope.key, envelope.key);
        assert_eq!(loaded_envelope.parent_key_meta, envelope.parent_key_meta);
        assert_eq!(loaded_envelope.revoked, envelope.revoked);
    }

    #[tokio::test]
    async fn test_postgres_metastore_load_latest() {
        let (pool, _docker) = setup_test_db().await;
        let metastore = PostgresMetastore::new(pool);

        // Create test data
        let id = "test_key_id_latest";

        // Create multiple keys with different timestamps
        let created1 = Utc::now().timestamp() - 100;
        let envelope1 = EnvelopeKeyRecord {
            created: created1,
            key: "old_encrypted_key".to_string(),
            parent_key_meta: None,
            revoked: false,
        };

        let created2 = Utc::now().timestamp();
        let envelope2 = EnvelopeKeyRecord {
            created: created2,
            key: "latest_encrypted_key".to_string(),
            parent_key_meta: None,
            revoked: false,
        };

        // Store keys
        let _ = metastore.store(id, created1, &envelope1).await.unwrap();
        let _ = metastore.store(id, created2, &envelope2).await.unwrap();

        // Load latest key
        let load_result = metastore.load_latest(id).await;
        assert!(load_result.is_ok());

        let loaded_envelope = load_result.unwrap();
        assert!(loaded_envelope.is_some());

        let loaded_envelope = loaded_envelope.unwrap();
        // The latest key should be the one with the higher timestamp
        assert_eq!(loaded_envelope.created, created2);
        assert_eq!(loaded_envelope.key, "latest_encrypted_key");
    }

    #[tokio::test]
    async fn test_postgres_metastore_duplicate_key() {
        let (pool, _docker) = setup_test_db().await;
        let metastore = PostgresMetastore::new(pool);

        // Create test data
        let id = "test_duplicate_key";
        let created = Utc::now().timestamp();
        let envelope = EnvelopeKeyRecord {
            created: created,
            key: "encrypted_key_data".to_string(),
            parent_key_meta: None,
            revoked: false,
        };

        // Store key first time
        let store_result = metastore.store(id, created, &envelope).await;
        assert!(store_result.is_ok());
        assert!(store_result.unwrap());

        // Try to store the same key again
        let duplicate_result = metastore.store(id, created, &envelope).await;
        assert!(duplicate_result.is_ok());
        assert!(!duplicate_result.unwrap()); // Should return false for duplicate
    }
}