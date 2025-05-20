use crate::envelope::EnvelopeKeyRecord;
use crate::error::Result;
use crate::Metastore;

use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};

/// An in-memory implementation of the Metastore trait
///
/// This implementation stores keys in memory, which is useful for testing
/// but should not be used in production as keys will be lost when the
/// process terminates.
pub struct InMemoryMetastore {
    /// Storage for keys: map of ID -> map of created -> EnvelopeKeyRecord
    store: Arc<RwLock<HashMap<String, HashMap<i64, EnvelopeKeyRecord>>>>,
}

impl InMemoryMetastore {
    /// Creates a new InMemoryMetastore
    pub fn new() -> Self {
        Self {
            store: Arc::new(RwLock::new(HashMap::new())),
        }
    }
}

impl Default for InMemoryMetastore {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait]
impl Metastore for InMemoryMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let store = self.store.read().unwrap();

        if let Some(id_map) = store.get(id) {
            if let Some(record) = id_map.get(&created) {
                return Ok(Some(record.clone()));
            }
        }

        Ok(None)
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let store = self.store.read().unwrap();

        if let Some(id_map) = store.get(id) {
            // Find the record with the highest created timestamp
            let latest = id_map
                .iter()
                .max_by_key(|(k, _)| *k)
                .map(|(_, v)| v.clone());

            return Ok(latest);
        }

        Ok(None)
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let mut store = self.store.write().unwrap();

        // Get or create the map for this ID
        let id_map = store.entry(id.to_string()).or_default();

        // Check if a record already exists
        if id_map.contains_key(&created) {
            return Ok(false);
        }

        // Store the record
        id_map.insert(created, envelope.clone());

        Ok(true)
    }
}
