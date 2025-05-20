use crate::envelope::EnvelopeKeyRecord;
use crate::error::Result;
use crate::Metastore;

use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};

/// An in-memory implementation of the Metastore trait
///
/// This implementation is meant for testing and should not be used in production.
pub struct MemoryMetastore {
    /// Map of key ID -> map of created timestamp -> envelope key record
    envelopes: Arc<RwLock<HashMap<String, HashMap<i64, EnvelopeKeyRecord>>>>,
}

impl MemoryMetastore {
    /// Creates a new MemoryMetastore
    pub fn new() -> Self {
        Self {
            envelopes: Arc::new(RwLock::new(HashMap::new())),
        }
    }
}

impl Default for MemoryMetastore {
    fn default() -> Self {
        Self::new()
    }
}

#[async_trait]
impl Metastore for MemoryMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let envelopes = self.envelopes.read().unwrap();
        
        if let Some(key_map) = envelopes.get(id) {
            if let Some(record) = key_map.get(&created) {
                return Ok(Some(record.clone()));
            }
        }
        
        Ok(None)
    }
    
    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let envelopes = self.envelopes.read().unwrap();
        
        if let Some(key_map) = envelopes.get(id) {
            if key_map.is_empty() {
                return Ok(None);
            }
            
            // Find the latest created timestamp
            let latest_created = key_map.keys()
                .max()
                .cloned()
                .unwrap_or(0);
                
            if let Some(record) = key_map.get(&latest_created) {
                return Ok(Some(record.clone()));
            }
        }
        
        Ok(None)
    }
    
    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let mut envelopes = self.envelopes.write().unwrap();
        
        // Get or create the key map
        let key_map = envelopes
            .entry(id.to_string())
            .or_default();
            
        // Check if the key already exists
        if key_map.contains_key(&created) {
            return Ok(false);
        }
        
        // Store the key
        key_map.insert(created, envelope.clone());
        
        Ok(true)
    }
}