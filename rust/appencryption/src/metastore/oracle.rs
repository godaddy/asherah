//! Oracle database implementation of the Metastore trait

use crate::metastore::memory::InMemoryMetastore;
use crate::{EnvelopeKeyRecord, Metastore, Result};
use async_trait::async_trait;
use std::sync::Arc;

/// Oracle metastore implementation
///
/// NOTE: This is a placeholder implementation for feature parity with Go.
/// A full implementation would require an Oracle database driver for Rust.
/// Currently implemented as a wrapper around InMemoryMetastore for compatibility.
pub struct OracleMetastore {
    // In a real implementation, this would be an Oracle connection pool
    // For now, we use InMemoryMetastore to maintain API compatibility
    inner: Arc<InMemoryMetastore>,
}

impl OracleMetastore {
    /// Create a new Oracle metastore
    ///
    /// # Arguments
    /// * `_connection_string` - Oracle connection string (currently unused)
    ///
    /// # Example
    /// ```ignore
    /// let metastore = OracleMetastore::new("user/password@localhost:1521/XEPDB1");
    /// ```
    pub fn new(_connection_string: &str) -> Self {
        // TODO: Implement actual Oracle connection when a suitable driver is available
        Self {
            inner: Arc::new(InMemoryMetastore::default()),
        }
    }
}

#[async_trait]
impl Metastore for OracleMetastore {
    async fn load(&self, key_id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        self.inner.load(key_id, created).await
    }

    async fn load_latest(&self, key_id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        self.inner.load_latest(key_id).await
    }

    async fn store(
        &self,
        key_id: &str,
        created: i64,
        envelope: &EnvelopeKeyRecord,
    ) -> Result<bool> {
        // Ensure we return a bool, not an Option<bool>
        let result = self.inner.store(key_id, created, envelope).await?;
        Ok(result)
    }
}

#[cfg(feature = "oracle")]
#[cfg(test)]
mod tests {
    use super::*;
    use crate::KeyMeta;

    #[tokio::test]
    async fn test_oracle_metastore_creation() {
        let metastore = OracleMetastore::new("test_connection_string");
        // Just check that we can create the metastore and do a basic operation
        assert!(metastore.inner.load("test", 0).await.is_ok());
    }

    #[tokio::test]
    async fn test_oracle_metastore_placeholder_functionality() {
        let metastore = OracleMetastore::new("test_connection_string");

        // Create a test envelope
        let envelope = EnvelopeKeyRecord {
            id: "test_key".to_string(),
            created: 1234567890,
            parent_key_meta: Some(KeyMeta {
                id: "parent_key".to_string(),
                created: 1234567890,
            }),
            encrypted_key: vec![1, 2, 3, 4],
            revoked: Some(false),
        };

        // Store should work (using in-memory implementation)
        let stored = metastore.store("test_key", 1234567890, &envelope).await;
        assert!(stored.is_ok());
        assert!(stored.unwrap());

        // Load should retrieve the stored envelope
        let loaded = metastore.load("test_key", 1234567890).await;
        assert!(loaded.is_ok());
        assert!(loaded.as_ref().unwrap().is_some());

        // Load latest should also work
        let latest = metastore.load_latest("test_key").await;
        assert!(latest.is_ok());
        assert!(latest.unwrap().is_some());
    }
}
