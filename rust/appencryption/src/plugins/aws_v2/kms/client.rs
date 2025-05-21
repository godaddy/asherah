use crate::error::{Error, Result};
use crate::timer;
use crate::Aead;
use crate::KeyManagementService;
use async_trait::async_trait;
use aws_sdk_kms::types::{DataKeySpec, EncryptionAlgorithmSpec};
use aws_sdk_kms::Client as AwsSdkKmsClient;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::Arc;

/// Response from the GenerateDataKey operation
#[derive(Clone, Debug)]
pub struct GenerateDataKeyResponse {
    /// The Amazon Resource Name (ARN) of the CMK that encrypted the data key
    pub key_id: String,

    /// The encrypted data key
    pub ciphertext_blob: Vec<u8>,

    /// The plaintext data key
    pub plaintext: Vec<u8>,
}

/// AWS KMS client trait
#[async_trait]
pub trait AwsKmsClient: Send + Sync {
    /// Encrypts data using a KMS key
    async fn encrypt(&self, key_id: &str, plaintext: &[u8]) -> Result<Vec<u8>>;

    /// Decrypts data that was encrypted with a KMS key
    async fn decrypt(&self, key_id: &str, ciphertext: &[u8]) -> Result<Vec<u8>>;

    /// Generates a data key using a KMS key
    async fn generate_data_key(&self, key_id: &str) -> Result<GenerateDataKeyResponse>;

    /// Returns the region for this client
    fn region(&self) -> &str;
}

/// Standard implementation of AwsKmsClient using AWS SDK v2
pub struct StandardAwsKmsClient {
    /// AWS SDK KMS client
    client: AwsSdkKmsClient,

    /// AWS region for this client
    region: String,
}

impl StandardAwsKmsClient {
    /// Creates a new StandardAwsKmsClient
    pub fn new(client: AwsSdkKmsClient, region: String) -> Self {
        Self { client, region }
    }
}

#[async_trait]
impl AwsKmsClient for StandardAwsKmsClient {
    async fn encrypt(&self, key_id: &str, plaintext: &[u8]) -> Result<Vec<u8>> {
        let result = self
            .client
            .encrypt()
            .key_id(key_id)
            .encryption_algorithm(EncryptionAlgorithmSpec::SymmetricDefault)
            .plaintext(aws_sdk_kms::primitives::Blob::new(plaintext.to_vec()))
            .send()
            .await
            .map_err(|e| Error::Kms(format!("KMS encrypt error: {}", e)))?;

        result
            .ciphertext_blob()
            .map(|b| b.as_ref().to_vec())
            .ok_or_else(|| Error::Kms("No ciphertext blob returned from KMS".into()))
    }

    async fn decrypt(&self, key_id: &str, ciphertext: &[u8]) -> Result<Vec<u8>> {
        let result = self
            .client
            .decrypt()
            .key_id(key_id)
            .encryption_algorithm(EncryptionAlgorithmSpec::SymmetricDefault)
            .ciphertext_blob(aws_sdk_kms::primitives::Blob::new(ciphertext.to_vec()))
            .send()
            .await
            .map_err(|e| Error::Kms(format!("KMS decrypt error: {}", e)))?;

        result
            .plaintext()
            .map(|b| b.as_ref().to_vec())
            .ok_or_else(|| Error::Kms("No plaintext returned from KMS".into()))
    }

    async fn generate_data_key(&self, key_id: &str) -> Result<GenerateDataKeyResponse> {
        let result = self
            .client
            .generate_data_key()
            .key_id(key_id)
            .key_spec(DataKeySpec::Aes256)
            .send()
            .await
            .map_err(|e| Error::Kms(format!("KMS generate data key error: {}", e)))?;

        let key_id = result.key_id().unwrap_or(key_id).to_string();
        let ciphertext_blob = result
            .ciphertext_blob()
            .map(|b| b.as_ref().to_vec())
            .ok_or_else(|| Error::Kms("No ciphertext blob returned from KMS".into()))?;
        let plaintext = result
            .plaintext()
            .map(|b| b.as_ref().to_vec())
            .ok_or_else(|| Error::Kms("No plaintext returned from KMS".into()))?;

        Ok(GenerateDataKeyResponse {
            key_id,
            ciphertext_blob,
            plaintext,
        })
    }

    fn region(&self) -> &str {
        &self.region
    }
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

impl std::fmt::Debug for RegionalClient {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("RegionalClient")
            .field("region", &self.region)
            .field("master_key_arn", &self.master_key_arn)
            .finish()
    }
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

        self.client.generate_data_key(&self.master_key_arn).await
    }

    /// Encrypts a key using the master key
    pub async fn encrypt_key(&self, key_bytes: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.encryptkey");

        self.client.encrypt(&self.master_key_arn, key_bytes).await
    }

    /// Decrypts a key using the master key
    pub async fn decrypt_key(&self, encrypted_key: &[u8]) -> Result<Vec<u8>> {
        let _timer = timer!("ael.kms.aws.decryptkey");

        self.client
            .decrypt(&self.master_key_arn, encrypted_key)
            .await
    }
}

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

/// AWS KMS implementation of the KeyManagementService trait
#[derive(Debug)]
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

    /// Returns the preferred region for this KMS
    pub fn preferred_region(&self) -> &str {
        &self.clients[0].region
    }

    /// Generates a data key for encrypting a system key
    async fn generate_data_key(&self) -> Result<GenerateDataKeyResponse> {
        for client in &self.clients {
            match client.generate_data_key().await {
                Ok(resp) => return Ok(resp),
                Err(e) => {
                    log::debug!(
                        "Error generating data key in region ({}), trying next region: {}",
                        client.region,
                        e
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
    async fn encrypt_regional_keks(&self, data_key: &GenerateDataKeyResponse) -> Vec<RegionalKek> {
        let (tx, mut rx) = tokio::sync::mpsc::channel(self.clients.len());

        // Encrypt in all regions concurrently
        for client in &self.clients {
            // If the key is already encrypted with the master key, add it directly
            if client.master_key_arn == data_key.key_id {
                let kek = RegionalKek {
                    region: client.region.clone(),
                    arn: client.master_key_arn.clone(),
                    encrypted_kek: data_key.ciphertext_blob.clone(),
                };

                let tx_clone = tx.clone();
                tokio::spawn(async move {
                    drop(tx_clone.send(kek).await);
                });

                continue;
            }

            let client_clone = client.clone();
            let plaintext = data_key.plaintext.clone();
            let tx_clone = tx.clone();

            tokio::spawn(async move {
                match client_clone.encrypt_key(&plaintext).await {
                    Ok(encrypted_key) => {
                        let kek = RegionalKek {
                            region: client_clone.region,
                            arn: client_clone.master_key_arn,
                            encrypted_kek: encrypted_key,
                        };

                        drop(tx_clone.send(kek).await);
                    }
                    Err(e) => {
                        log::debug!(
                            "Error encrypting data key in region ({}): {}",
                            client_clone.region,
                            e
                        );
                    }
                }
            });
        }

        // Drop the sender so the channel will close when all tasks complete
        drop(tx);

        // Collect results
        let mut result = Vec::new();
        while let Some(kek) = rx.recv().await {
            result.push(kek);
        }

        result
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
            let mut plaintext = data_key.plaintext.clone();

            // Encrypt the key with the data key
            let enc_key_bytes = self
                .crypto
                .encrypt(key, &plaintext)
                .map_err(|e| Error::Kms(format!("Error encrypting key: {}", e)))?;

            // Securely wipe the plaintext data key
            plaintext.iter_mut().for_each(|b| *b = 0);

            // Encrypt the data key in all regions
            let keks = self.encrypt_regional_keks(&data_key).await;

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
                                log::debug!("Error crypto decrypt: {}", e);
                                continue;
                            }
                        }
                    }
                    Err(e) => {
                        log::debug!("Error KMS decrypt: {}", e);
                        continue;
                    }
                }
            } else {
                log::debug!("No KEK found for region: {}", client.region);
            }
        }

        Err(Error::Kms("Decrypt failed in all regions".into()))
    }
}
