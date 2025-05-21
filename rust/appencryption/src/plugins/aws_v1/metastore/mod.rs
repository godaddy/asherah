//! DynamoDB metastore implementation using AWS SDK v1
//!
//! This module provides an implementation of the `Metastore` trait using DynamoDB with AWS SDK v1.
//! It supports both standard and global table configurations.

use crate::envelope::{EnvelopeKeyRecord, KeyMeta};
use crate::error::{Error, Result};
use crate::timer;
use crate::Metastore;
use async_trait::async_trait;
use base64::Engine;
use std::sync::Arc;

mod client;
pub use client::{DynamoDbClient, DynamoDbConfig, StandardDynamoDbClient};
pub use client::{DynamoDbEnvelope, DynamoDbItem, DynamoDbKey, DynamoDbKeyMeta};

// Default table name for the DynamoDB metastore
const DEFAULT_TABLE_NAME: &str = "EncryptionKey";

/// DynamoDB metastore implementation
#[derive(Debug)]
pub struct DynamoDbMetastore {
    /// DynamoDB client
    client: Arc<dyn DynamoDbClient>,

    /// Table name for the metastore
    table_name: String,

    /// Region suffix for global tables
    region_suffix: Option<String>,
}

impl DynamoDbMetastore {
    /// Creates a new DynamoDbMetastore with the given client
    pub fn new(
        client: Arc<dyn DynamoDbClient>,
        table_name: Option<String>,
        use_region_suffix: bool,
    ) -> Self {
        let table_name = table_name.unwrap_or_else(|| DEFAULT_TABLE_NAME.to_string());

        let region_suffix = if use_region_suffix {
            Some(client.region().to_string())
        } else {
            None
        };

        Self {
            client,
            table_name,
            region_suffix,
        }
    }

    /// Creates a new DynamoDbMetastore with default configuration
    pub fn new_default(region: String) -> Result<Self> {
        let client = StandardDynamoDbClient::new(region)?;
        Ok(Self::new(Arc::new(client), None, false))
    }

    /// Creates a new DynamoDbMetastore with global table support
    pub fn new_with_global_table(region: String, table_name: Option<String>) -> Result<Self> {
        let client = StandardDynamoDbClient::new(region)?;
        Ok(Self::new(Arc::new(client), table_name, true))
    }

    /// Returns the table name for this metastore
    pub fn table_name(&self) -> &str {
        &self.table_name
    }

    /// Returns the region suffix for this metastore (if used)
    pub fn region_suffix(&self) -> Option<&str> {
        self.region_suffix.as_deref()
    }

    /// Returns the DynamoDB client for this metastore
    pub fn client(&self) -> &Arc<dyn DynamoDbClient> {
        &self.client
    }

    /// Gets the region-specific ID for a global table
    fn get_id_with_suffix(&self, id: &str) -> String {
        if let Some(suffix) = &self.region_suffix {
            format!("{}_{}", id, suffix)
        } else {
            id.to_string()
        }
    }

    /// Converts a DynamoDB item to an EnvelopeKeyRecord
    fn decode_item(&self, item: DynamoDbItem) -> Result<EnvelopeKeyRecord> {
        // Create the key record
        let mut ekr = EnvelopeKeyRecord {
            revoked: item.key_record.revoked,
            id: item.id,
            created: item.key_record.created,
            encrypted_key: base64::engine::general_purpose::STANDARD
                .decode(&item.key_record.encrypted_key)
                .map_err(|e| Error::Metastore(format!("Failed to decode encrypted key: {}", e)))?,
            parent_key_meta: None,
        };

        // Add the parent key metadata if present
        if let Some(km) = item.key_record.parent_key_meta {
            ekr.parent_key_meta = Some(KeyMeta {
                id: km.id,
                created: km.created,
            });
        }

        Ok(ekr)
    }
}

#[async_trait]
impl Metastore for DynamoDbMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.dynamodb.load");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Create the key
        let key = DynamoDbKey {
            id: id_with_suffix,
            created,
        };

        // Get the item from DynamoDB
        let item = self.client.get_item(&self.table_name, key).await?;

        // Decode the item if it exists
        if let Some(item) = item {
            Ok(Some(self.decode_item(item)?))
        } else {
            Ok(None)
        }
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.dynamodb.loadlatest");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Query DynamoDB for the latest item
        let items = self
            .client
            .query_latest(&self.table_name, &id_with_suffix)
            .await?;

        // Decode the item if it exists
        if let Some(item) = items.into_iter().next() {
            Ok(Some(self.decode_item(item)?))
        } else {
            Ok(None)
        }
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let _timer = timer!("ael.metastore.dynamodb.store");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Create the key metadata
        let parent_key_meta = envelope.parent_key_meta.as_ref().map(|km| DynamoDbKeyMeta {
            id: km.id.clone(),
            created: km.created,
        });

        // Create the envelope
        let db_envelope = DynamoDbEnvelope {
            revoked: envelope.revoked,
            created: envelope.created,
            encrypted_key: base64::engine::general_purpose::STANDARD
                .encode(&envelope.encrypted_key),
            parent_key_meta,
        };

        // Create the item
        let item = DynamoDbItem {
            id: id_with_suffix,
            created,
            key_record: db_envelope,
        };

        // Put the item in DynamoDB
        self.client
            .put_item_if_not_exists(&self.table_name, item)
            .await
    }
}
