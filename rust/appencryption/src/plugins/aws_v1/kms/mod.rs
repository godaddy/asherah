//! AWS KMS implementation using AWS SDK v1
//!
//! This module provides an implementation of the `KeyManagementService` trait using AWS KMS with SDK v1.
//! It supports multi-region encryption and decryption for high availability and resilience.

use crate::error::{Error, Result};
use crate::timer;
use crate::Aead;
use crate::KeyManagementService;
use async_trait::async_trait;
use log::debug;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::Arc;
use zeroize::Zeroizing;

mod client;
pub use client::{AwsKmsClient, GenerateDataKeyResponse, StandardAwsKmsClient};

/// Regional key encryption key
#[derive(Debug, Clone, Serialize, Deserialize)]
struct RegionalKek {
    /// The region for this KEK
    region: String,

    /// The ARN of the master key
    arn: String,

    /// The encrypted key encryption key
    #[serde(rename = "encryptedKek")]
    encrypted_kek: Vec<u8>,
}

/// Envelope for encrypted keys
#[derive(Debug, Clone, Serialize, Deserialize)]
struct Envelope {
    /// The encrypted key
    #[serde(rename = "encryptedKey")]
    encrypted_key: Vec<u8>,

    /// The key encryption keys for each region
    #[serde(rename = "kmsKeks")]
    keks: Vec<RegionalKek>,
}

/// Regional client for KMS operations
#[derive(Clone)]
pub struct RegionalClient {
    /// The KMS client for a specific region
    client: Arc<dyn AwsKmsClient>,

    /// The region for this client
    pub(crate) region: String,

    /// The ARN of the master key
    pub(crate) master_key_arn: String,
}

impl RegionalClient {
    /// Creates a new RegionalClient
    pub fn new(client: Arc<dyn AwsKmsClient>, master_key_arn: String) -> Self {
        let region = client.region().to_string();
        Self {
            client,
            region,
            master_key_arn,
        }
    }

    /// Generates a data key using the master key
    pub async fn generate_data_key(&self) -> Result<GenerateDataKeyResponse> {
        let _timer = timer!("ael.kms.aws.generatedatakey", "region" => self.region.clone());

        let result = self.client.generate_data_key(&self.master_key_arn).await;

        result
    }

    /// Encrypts a key using the master key
    pub async fn encrypt_key(&self, key_bytes: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.encryptkey");

        let result = self.client.encrypt(&self.master_key_arn, key_bytes).await;

        result
    }

    /// Decrypts a key using the master key
    pub async fn decrypt_key(&self, encrypted_key: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.decryptkey");

        let result = self.client.decrypt(encrypted_key).await;

        result
    }
}

/// AWS KMS implementation
pub struct AwsKms {
    /// Regional clients for KMS operations
    clients: Vec<RegionalClient>,

    /// AEAD implementation for data encryption
    crypto: Arc<dyn Aead>,
}

impl AwsKms {
    /// Creates a new AwsKms with the given clients and crypto implementation
    pub fn new(clients: Vec<RegionalClient>, crypto: Arc<dyn Aead>) -> Self {
        Self { clients, crypto }
    }

    /// Creates a new AwsKms with clients for the specified regions and ARNs
    pub fn from_region_map(
        region_arn_map: HashMap<String, String>,
        preferred_region: &str,
        crypto: Arc<dyn Aead>,
    ) -> Result<Self> {
        let mut clients = Vec::with_capacity(region_arn_map.len());

        // Create clients for each region
        for (region, arn) in region_arn_map {
            let kms_client = StandardAwsKmsClient::new(region.clone())?;
            let client = RegionalClient::new(Arc::new(kms_client), arn);
            clients.push(client);
        }

        // Sort clients so the preferred region is first
        clients.sort_by(|a, b| {
            if a.region == preferred_region {
                std::cmp::Ordering::Less
            } else if b.region == preferred_region {
                std::cmp::Ordering::Greater
            } else {
                a.region.cmp(&b.region)
            }
        });

        Ok(Self::new(clients, crypto))
    }

    /// Returns the preferred region for this KMS
    pub fn preferred_region(&self) -> &str {
        if self.clients.is_empty() {
            ""
        } else {
            &self.clients[0].region
        }
    }

    /// Generates a data key for encrypting a system key
    async fn generate_data_key(&self) -> Result<GenerateDataKeyResponse> {
        for client in &self.clients {
            match client.generate_data_key().await {
                Ok(resp) => return Ok(resp),
                Err(e) => {
                    debug!(
                        "Error generating data key in region ({}), trying next region: {}",
                        client.region, e
                    );
                    continue;
                }
            }
        }

        Err(Error::Kms(
            "All regions returned errors when generating data key".into(),
        ))
    }

    /// Encrypts a data key in all regions
    async fn encrypt_in_all_regions(&self, data_key: &GenerateDataKeyResponse) -> Vec<RegionalKek> {
        let mut results = Vec::with_capacity(self.clients.len());
        let mut handles = Vec::with_capacity(self.clients.len());

        // Encrypt in all regions concurrently
        for client in &self.clients {
            // If the key is already encrypted with the master key, add it directly
            if client.master_key_arn == data_key.key_id {
                results.push(RegionalKek {
                    region: client.region.clone(),
                    arn: client.master_key_arn.clone(),
                    encrypted_kek: data_key.ciphertext_blob.clone(),
                });
                continue;
            }

            let client_clone = client.clone();
            let plaintext = data_key.plaintext.clone();

            let handle = tokio::spawn(async move {
                match client_clone.encrypt_key(&plaintext).await {
                    Ok(encrypted_key) => Some(RegionalKek {
                        region: client_clone.region,
                        arn: client_clone.master_key_arn,
                        encrypted_kek: encrypted_key,
                    }),
                    Err(e) => {
                        debug!(
                            "Error encrypting data key in region ({}): {}",
                            client_clone.region, e
                        );
                        None
                    }
                }
            });

            handles.push(handle);
        }

        // Wait for all encryptions to complete
        for handle in handles {
            if let Ok(Some(kek)) = handle.await {
                results.push(kek);
            }
        }

        results
    }
}

#[async_trait]
impl KeyManagementService for AwsKms {
    async fn encrypt_key(&self, key: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.encryptkey");

        // Generate a data key to encrypt the key
        let data_key = self.generate_data_key().await?;

        // Create a closure to ensure we securely wipe the plaintext data key
        {
            let plaintext = Zeroizing::new(data_key.plaintext.clone());

            // Encrypt the key with the data key
            let enc_key_bytes = self
                .crypto
                .encrypt(key, &plaintext)
                .map_err(|e| Error::Kms(format!("Error encrypting key: {}", e)))?;

            // Encrypt the data key in all regions
            let keks = self.encrypt_in_all_regions(&data_key).await;

            // Create the envelope
            let envelope = Envelope {
                encrypted_key: enc_key_bytes,
                keks,
            };

            // Serialize the envelope to JSON
            serde_json::to_vec(&envelope)
                .map_err(|e| Error::Kms(format!("Error marshalling envelope: {}", e)))
        }
    }

    async fn decrypt_key(&self, encrypted_key: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.decryptkey");

        // Deserialize the envelope
        let envelope: Envelope = serde_json::from_slice(encrypted_key)
            .map_err(|e| Error::Kms(format!("Unable to unmarshal envelope: {}", e)))?;

        // Create a map of region -> KEK for easy lookup
        let keks: HashMap<String, RegionalKek> = envelope
            .keks
            .into_iter()
            .map(|kek| (kek.region.clone(), kek))
            .collect();

        // Try each region in order of preference
        for client in &self.clients {
            // Check if we have a KEK for this region
            if let Some(kek) = keks.get(&client.region) {
                // Try to decrypt the KEK
                match client.decrypt_key(&kek.encrypted_kek).await {
                    Ok(plaintext) => {
                        // Use the plaintext data key to decrypt the encrypted key
                        match self.crypto.decrypt(&envelope.encrypted_key, &plaintext) {
                            Ok(key_bytes) => {
                                return Ok(key_bytes);
                            }
                            Err(e) => {
                                debug!("Error crypto decrypt: {}", e);
                                continue;
                            }
                        }
                    }
                    Err(e) => {
                        debug!("Error KMS decrypt: {}", e);
                        continue;
                    }
                }
            }
        }

        Err(Error::Kms("Decrypt failed in all regions".into()))
    }
}
