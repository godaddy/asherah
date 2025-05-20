// Expanded PostgreSQL integration tests

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID, PRODUCT,
    SERVICE,
};
use appencryption::{
    envelope::EnvelopeKeyRecord,
    // Using InMemoryMetastore as a placeholder for PostgresMetastore
    metastore::InMemoryMetastore,
    KeyMeta,
    Metastore,
    Session,
    SessionFactory,
};
use chrono::{Duration, Utc};
use std::sync::Arc;

// NOTE: The actual PostgreSQL integration tests are not implemented yet.
// They would require setting up PostgreSQL containers and proper SQL metastore implementation.
// For now, we provide placeholder tests using InMemoryMetastore.

#[tokio::test]
async fn test_postgres_metastore_placeholder() {
    // This is a placeholder test
    // Real PostgreSQL integration tests would use testcontainers with PostgreSQL
    let metastore = Arc::new(InMemoryMetastore::new());

    // Basic test that metastore works
    let key_id = "test_key";
    let created = Utc::now().timestamp();
    let envelope = EnvelopeKeyRecord {
        id: key_id.to_string(),
        created,
        encrypted_key: vec![1, 2, 3, 4],
        revoked: None,
        parent_key_meta: None,
    };

    // Store and load
    metastore
        .store(key_id, created, &envelope)
        .await
        .expect("Store failed");
    let loaded = metastore.load(key_id, created).await.expect("Load failed");
    assert!(loaded.is_some());
}
