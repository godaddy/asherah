//! Envelope encryption logic for the application encryption library
//!
//! This module contains the implementation of envelope encryption using a hierarchical
//! key model (System Keys, Intermediate Keys, Data Row Keys).

pub mod encryption;

use serde::{Deserialize, Serialize};

/// Metadata for a key including its ID and creation timestamp
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct KeyMeta {
    #[serde(rename = "KeyId")]
    pub id: String,
    #[serde(rename = "Created")]
    pub created: i64,
}

impl KeyMeta {
    /// Creates a new KeyMeta
    pub fn new(id: String, created: i64) -> Self {
        KeyMeta { id, created }
    }

    /// Returns true if this is the latest version of the key (created == 0)
    pub fn is_latest(&self) -> bool {
        self.created == 0
    }

    /// Returns a copy of this KeyMeta as the latest version (created = 0)
    pub fn as_latest(&self) -> KeyMeta {
        KeyMeta {
            id: self.id.clone(),
            created: 0,
        }
    }
}

/// Record containing encrypted key and metadata
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct EnvelopeKeyRecord {
    #[serde(rename = "Revoked", skip_serializing_if = "Option::is_none")]
    pub revoked: Option<bool>,
    #[serde(skip)]
    pub id: String,
    #[serde(rename = "Created")]
    pub created: i64,
    #[serde(rename = "Key")]
    pub encrypted_key: Vec<u8>,
    #[serde(rename = "ParentKeyMeta", skip_serializing_if = "Option::is_none")]
    pub parent_key_meta: Option<KeyMeta>,
}

/// Record containing encrypted data and key
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DataRowRecord {
    #[serde(rename = "Key")]
    pub key: EnvelopeKeyRecord,
    #[serde(rename = "Data")]
    pub data: Vec<u8>,
}

pub use encryption::EnvelopeEncryption;
