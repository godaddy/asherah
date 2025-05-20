use crate::envelope::{DataRowRecord, EnvelopeKeyRecord, KeyMeta};
use crate::error::{Error, Result};
use crate::key::cache::KeyCacher;
use crate::key::CryptoKey;
use crate::partition::Partition;
use crate::policy::CryptoPolicy;
use crate::util;
use crate::Aead;
use crate::Encryption;
use crate::KeyManagementService;
use crate::Metastore;
use crate::AES256_KEY_SIZE;

use async_trait::async_trait;
use chrono::Utc;
use metrics::{counter, histogram};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::time::Instant;

/// Implementation of envelope encryption
pub struct EnvelopeEncryption {
    /// Partition for key separation
    partition: Arc<dyn Partition>,

    /// Metastore for key persistence
    metastore: Arc<dyn Metastore>,

    /// Key Management Service for key encryption
    kms: Arc<dyn KeyManagementService>,

    /// Crypto policy for key management
    policy: Arc<CryptoPolicy>,

    /// AEAD implementation for data encryption
    crypto: Arc<dyn Aead>,

    /// Secret factory for creating secure secrets
    secret_factory: Arc<DefaultSecretFactory>,

    /// System key cache
    sk_cache: crate::key::cache::AnyCache,

    /// Intermediate key cache
    ik_cache: crate::key::cache::AnyCache,
}

/// Builder for EnvelopeEncryption
#[derive(Default)]
pub struct EnvelopeEncryptionBuilder {
    partition: Option<Arc<dyn Partition>>,
    metastore: Option<Arc<dyn Metastore>>,
    kms: Option<Arc<dyn KeyManagementService>>,
    policy: Option<Arc<CryptoPolicy>>,
    crypto: Option<Arc<dyn Aead>>,
    secret_factory: Option<Arc<DefaultSecretFactory>>,
    sk_cache: Option<crate::key::cache::AnyCache>,
    ik_cache: Option<crate::key::cache::AnyCache>,
}

impl EnvelopeEncryptionBuilder {
    /// Creates a new builder
    pub fn new() -> Self {
        Self::default()
    }

    /// Sets the partition
    pub fn with_partition(mut self, partition: Arc<dyn Partition>) -> Self {
        self.partition = Some(partition);
        self
    }

    /// Sets the metastore
    pub fn with_metastore(mut self, metastore: Arc<dyn Metastore>) -> Self {
        self.metastore = Some(metastore);
        self
    }

    /// Sets the KMS
    pub fn with_kms(mut self, kms: Arc<dyn KeyManagementService>) -> Self {
        self.kms = Some(kms);
        self
    }

    /// Sets the crypto policy
    pub fn with_policy(mut self, policy: Arc<CryptoPolicy>) -> Self {
        self.policy = Some(policy);
        self
    }

    /// Sets the crypto implementation
    pub fn with_crypto(mut self, crypto: Arc<dyn Aead>) -> Self {
        self.crypto = Some(crypto);
        self
    }

    /// Sets the secret factory
    pub fn with_secret_factory(mut self, secret_factory: Arc<DefaultSecretFactory>) -> Self {
        self.secret_factory = Some(secret_factory);
        self
    }

    /// Sets the system key cache
    pub fn with_sk_cache(mut self, sk_cache: crate::key::cache::AnyCache) -> Self {
        self.sk_cache = Some(sk_cache);
        self
    }

    /// Sets the intermediate key cache
    pub fn with_ik_cache(mut self, ik_cache: crate::key::cache::AnyCache) -> Self {
        self.ik_cache = Some(ik_cache);
        self
    }

    /// Builds the EnvelopeEncryption
    pub fn build(self) -> Result<EnvelopeEncryption> {
        let partition = self
            .partition
            .ok_or_else(|| Error::InvalidArgument("partition is required".to_string()))?;
        let metastore = self
            .metastore
            .ok_or_else(|| Error::InvalidArgument("metastore is required".to_string()))?;
        let kms = self
            .kms
            .ok_or_else(|| Error::InvalidArgument("kms is required".to_string()))?;
        let policy = self
            .policy
            .ok_or_else(|| Error::InvalidArgument("policy is required".to_string()))?;
        let crypto = self
            .crypto
            .ok_or_else(|| Error::InvalidArgument("crypto is required".to_string()))?;
        let secret_factory = self
            .secret_factory
            .ok_or_else(|| Error::InvalidArgument("secret_factory is required".to_string()))?;
        let sk_cache = self
            .sk_cache
            .ok_or_else(|| Error::InvalidArgument("sk_cache is required".to_string()))?;
        let ik_cache = self
            .ik_cache
            .ok_or_else(|| Error::InvalidArgument("ik_cache is required".to_string()))?;

        Ok(EnvelopeEncryption {
            partition,
            metastore,
            kms,
            policy,
            crypto,
            secret_factory,
            sk_cache,
            ik_cache,
        })
    }
}

impl EnvelopeEncryption {
    /// Creates a new EnvelopeEncryption
    #[deprecated(since = "0.1.1", note = "Use EnvelopeEncryptionBuilder instead")]
    pub fn new(
        partition: Arc<dyn Partition>,
        metastore: Arc<dyn Metastore>,
        kms: Arc<dyn KeyManagementService>,
        policy: Arc<CryptoPolicy>,
        crypto: Arc<dyn Aead>,
        secret_factory: Arc<DefaultSecretFactory>,
        sk_cache: crate::key::cache::AnyCache,
        ik_cache: crate::key::cache::AnyCache,
    ) -> Self {
        Self {
            partition,
            metastore,
            kms,
            policy,
            crypto,
            secret_factory,
            sk_cache,
            ik_cache,
        }
    }

    /// Creates a new EnvelopeEncryption using the builder pattern
    pub fn builder() -> EnvelopeEncryptionBuilder {
        EnvelopeEncryptionBuilder::new()
    }

    /// Loads or creates a cryptographic key
    async fn load_create_key(
        &self,
        id: &str,
        is_system_key: bool,
        parent_key: Option<Arc<CryptoKey>>,
    ) -> Result<Arc<crate::key::cache::CachedCryptoKey>> {
        // Create loader function
        let metastore = self.metastore.clone();
        let kms = self.kms.clone();
        let policy = self.policy.clone();
        let crypto = self.crypto.clone();
        let secret_factory = self.secret_factory.clone();
        let parent_key_clone = parent_key;

        let loader = move |meta: KeyMeta| {
            let metastore = metastore.clone();
            let kms = kms.clone();
            let policy = policy.clone();
            let crypto = crypto.clone();
            let secret_factory = secret_factory.clone();
            let parent_key = parent_key_clone.clone();

            async move {
                let (key_record, created) = if meta.is_latest() {
                    // Load the latest key first
                    if let Some(record) = metastore.load_latest(&meta.id).await? {
                        (Some(record), 0)
                    } else {
                        // No key found, create one with current timestamp
                        (
                            None,
                            crate::policy::new_key_timestamp(policy.create_date_precision),
                        )
                    }
                } else {
                    // Load specific key
                    (metastore.load(&meta.id, meta.created).await?, meta.created)
                };

                if let Some(record) = key_record {
                    // Key exists, decrypt it
                    let created = record.created;
                    let encrypted_key = record.encrypted_key.as_slice();
                    let key_bytes = if is_system_key {
                        // System key is encrypted with KMS
                        kms.decrypt_key(encrypted_key).await?
                    } else if let Some(parent) = parent_key {
                        // Intermediate key is encrypted with parent
                        parent.with_bytes(|parent_bytes| {
                            crypto.decrypt(encrypted_key, parent_bytes)
                        })?
                    } else {
                        return Err(Error::Internal(
                            "Parent key required for intermediate key decryption".into(),
                        ));
                    };

                    // Create the crypto key
                    let crypto_key = CryptoKey::new(
                        meta.id.clone(),
                        created,
                        key_bytes,
                        secret_factory.as_ref(),
                    )?;

                    // Check if revoked
                    if created < policy.revoked_before {
                        crypto_key.set_revoked(true);
                    }

                    Ok(crypto_key)
                } else if meta.is_latest() {
                    // Key doesn't exist, create a new one
                    let key_bytes = util::get_rand_bytes(AES256_KEY_SIZE);
                    let mut crypto_key = CryptoKey::new(
                        meta.id.clone(),
                        created,
                        key_bytes,
                        secret_factory.as_ref(),
                    )?;

                    // Encrypt the key
                    let encrypted_key = if is_system_key {
                        // System key is encrypted with KMS
                        crypto_key.with_bytes(|key_bytes| {
                            futures::executor::block_on(kms.encrypt_key(key_bytes))
                        })?
                    } else if let Some(parent) = parent_key.as_ref() {
                        // Intermediate key is encrypted with parent
                        crypto_key.with_bytes(|key_bytes| -> Result<Vec<u8>> {
                            parent
                                .with_bytes(|parent_bytes| crypto.encrypt(key_bytes, parent_bytes))
                        })?
                    } else {
                        return Err(Error::Internal(
                            "Parent key required for intermediate key encryption".into(),
                        ));
                    };

                    // Create key record
                    let parent_key_meta = if is_system_key {
                        None
                    } else {
                        parent_key.as_ref().map(|pk| KeyMeta {
                            id: pk.id().to_string(),
                            created: pk.created(),
                        })
                    };

                    let key_record = EnvelopeKeyRecord {
                        created,
                        encrypted_key,
                        id: meta.id.clone(),
                        revoked: None,
                        parent_key_meta,
                    };

                    // Store the key
                    if !metastore.store(&meta.id, created, &key_record).await? {
                        // Key was created by another process, load it
                        let record = metastore.load(&meta.id, created).await?.ok_or_else(|| {
                            Error::Internal("Failed to load key after creation conflict".into())
                        })?;

                        let key_bytes = if is_system_key {
                            kms.decrypt_key(record.encrypted_key.as_slice()).await?
                        } else if let Some(parent) = parent_key.as_ref() {
                            parent.with_bytes(|parent_bytes| {
                                crypto.decrypt(record.encrypted_key.as_slice(), parent_bytes)
                            })?
                        } else {
                            return Err(Error::Internal(
                                "Parent key required for intermediate key decryption".into(),
                            ));
                        };

                        crypto_key =
                            CryptoKey::new(meta.id, created, key_bytes, secret_factory.as_ref())?;
                    }

                    Ok(crypto_key)
                } else {
                    Err(Error::KeyNotFound(format!(
                        "Key {}:{} not found",
                        meta.id, meta.created
                    )))
                }
            }
        };

        // Get from cache or load
        if is_system_key {
            self.sk_cache.get_or_load_latest(id, loader).await
        } else {
            self.ik_cache.get_or_load_latest(id, loader).await
        }
    }

    /// Loads or creates a system key
    async fn get_system_key(&self) -> Result<Arc<crate::key::cache::CachedCryptoKey>> {
        let key_id = self.partition.system_key_id();

        // Get metrics timer
        let _timer = crate::timer!("ael.envelope.get_system_key");

        self.load_create_key(&key_id, true, None).await
    }

    /// Loads or creates an intermediate key
    async fn get_intermediate_key(
        &self,
        system_key: Arc<CryptoKey>,
    ) -> Result<Arc<crate::key::cache::CachedCryptoKey>> {
        let intermediate_key_id = self.partition.intermediate_key_id();

        // Get metrics timer
        let _timer = crate::timer!("ael.envelope.get_intermediate_key");

        self.load_create_key(&intermediate_key_id, false, Some(system_key))
            .await
    }

    /// Creates a data row record from a payload
    async fn create_data_row_record(&self, data: &[u8]) -> Result<DataRowRecord> {
        // Load system key -> intermediate key
        let system_key = self.get_system_key().await?;
        let intermediate_key = self
            .get_intermediate_key(system_key.crypto_key.clone())
            .await?;

        // Generate a new data key
        let data_key = util::get_rand_bytes(AES256_KEY_SIZE);

        // Encrypt data with data key
        let encrypted_data = self.crypto.encrypt(data, &data_key)?;

        // Encrypt data key with intermediate key
        let encrypted_data_key = intermediate_key
            .crypto_key
            .with_bytes(|key_bytes| self.crypto.encrypt(&data_key, key_bytes))?;

        // Create data row record
        Ok(DataRowRecord {
            key: EnvelopeKeyRecord {
                revoked: None,
                id: "".to_string(), // Data row keys don't have their own ID
                created: Utc::now().timestamp(),
                encrypted_key: encrypted_data_key,
                parent_key_meta: Some(KeyMeta {
                    id: intermediate_key.crypto_key.id().to_string(),
                    created: intermediate_key.crypto_key.created(),
                }),
            },
            data: encrypted_data,
        })
    }

    /// Loads an intermediate key with the given metadata
    async fn load_intermediate_key(
        &self,
        key_meta: &KeyMeta,
    ) -> Result<Arc<crate::key::cache::CachedCryptoKey>> {
        // Load system key first
        let system_key = self.get_system_key().await?;

        // Create a loader for an intermediate key with specific creation time
        let metastore = self.metastore.clone();
        let crypto = self.crypto.clone();
        let secret_factory = self.secret_factory.clone();
        let system_key_clone = system_key.crypto_key.clone();
        let policy = self.policy.clone();

        let loader = move |meta: KeyMeta| {
            let metastore = metastore.clone();
            let crypto = crypto.clone();
            let secret_factory = secret_factory.clone();
            let system_key = system_key_clone.clone();
            let policy = policy.clone();

            async move {
                // Load the key record
                let record = metastore
                    .load(&meta.id, meta.created)
                    .await?
                    .ok_or_else(|| {
                        Error::KeyNotFound(format!("Key {}:{} not found", meta.id, meta.created))
                    })?;

                // Decrypt with system key
                let key_bytes = system_key
                    .with_bytes(|sk_bytes| crypto.decrypt(&record.encrypted_key, sk_bytes))?;

                // Create crypto key
                let crypto_key = CryptoKey::new(
                    record.id.clone(),
                    record.created,
                    key_bytes,
                    secret_factory.as_ref(),
                )?;

                // Check if revoked
                if record.created < policy.revoked_before {
                    crypto_key.set_revoked(true);
                }

                Ok(crypto_key)
            }
        };

        // Load from cache or create
        self.ik_cache.get_or_load(key_meta.clone(), loader).await
    }
}

#[async_trait]
impl Encryption for EnvelopeEncryption {
    async fn encrypt_payload(&self, data: &[u8]) -> Result<DataRowRecord> {
        // Get metrics timer
        let start = Instant::now();

        // Increment encrypt counter
        counter!("ael.envelope.encrypt", 1);

        let result = self.create_data_row_record(data).await;
        histogram!("ael.envelope.encrypt.time", start.elapsed());
        result
    }

    async fn decrypt_data_row_record(&self, drr: &DataRowRecord) -> Result<Vec<u8>> {
        // Get metrics timer
        let start = Instant::now();

        // Increment decrypt counter
        counter!("ael.envelope.decrypt", 1);

        // Load intermediate key
        let parent_key_meta = drr.key.parent_key_meta.as_ref().ok_or_else(|| {
            Error::Internal("Missing parent key metadata in data row record".into())
        })?;

        // Validate the intermediate key ID belongs to this partition
        if !self
            .partition
            .is_valid_intermediate_key_id(&parent_key_meta.id)
        {
            return Err(Error::Crypto("Unable to decrypt record".into()));
        }

        let intermediate_key = self.load_intermediate_key(parent_key_meta).await?;

        // Decrypt data key
        let data_key = intermediate_key
            .crypto_key
            .with_bytes(|key_bytes| self.crypto.decrypt(&drr.key.encrypted_key, key_bytes))?;

        // Decrypt data
        let data = self.crypto.decrypt(&drr.data, &data_key)?;

        histogram!("ael.envelope.decrypt.time", start.elapsed());
        Ok(data)
    }

    async fn close(&self) -> Result<()> {
        Ok(())
    }

    fn as_any(&self) -> &dyn std::any::Any {
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::crypto::Aes256GcmAead;
    use crate::envelope::EnvelopeKeyRecord;
    use crate::key::cache::{CacheKeyType, KeyCache, NeverCache};
    use crate::key::CryptoKey;
    use crate::kms::StaticKeyManagementService;
    use crate::metastore::InMemoryMetastore;
    use crate::partition::DefaultPartition;
    use crate::policy::CryptoPolicy;
    use securememory::protected_memory::DefaultSecretFactory;
    use std::sync::Arc;
    use std::time::Duration;

    // Test encrypt and decrypt with envelope encryption
    #[tokio::test]
    async fn test_envelope_encryption() -> Result<()> {
        // Create components
        let kms = Arc::new(StaticKeyManagementService::new(vec![0; 32]));
        let metastore = Arc::new(InMemoryMetastore::new());
        let policy = Arc::new(CryptoPolicy::default());
        let crypto = Arc::new(Aes256GcmAead::new());
        let secret_factory = Arc::new(DefaultSecretFactory::new());
        let partition = Arc::new(DefaultPartition::new("test", "service", "product"));

        // Create caches
        let sk_cache = crate::key::cache::AnyCache::KeyCache(Arc::new(KeyCache::new(
            CacheKeyType::SystemKeys,
            policy.clone(),
        )));
        let ik_cache = crate::key::cache::AnyCache::KeyCache(Arc::new(KeyCache::new(
            CacheKeyType::IntermediateKeys,
            policy.clone(),
        )));

        // Create envelope encryption
        let encryption = EnvelopeEncryption::new(
            partition,
            metastore.clone(),
            kms.clone(),
            policy.clone(),
            crypto.clone(),
            secret_factory.clone(),
            sk_cache,
            ik_cache,
        );

        // Test data
        let data = b"hello world";

        // Encrypt data
        let drr = encryption.encrypt_payload(data).await?;

        // Decrypt data
        let decrypted = encryption.decrypt_data_row_record(&drr).await?;

        // Verify
        assert_eq!(data, decrypted.as_slice());

        Ok(())
    }

    // Test key rotation
    #[tokio::test]
    async fn test_key_rotation() -> Result<()> {
        // Create components
        let kms = Arc::new(StaticKeyManagementService::new(vec![0; 32]));
        let metastore = Arc::new(InMemoryMetastore::new());
        let mut policy = CryptoPolicy::default();
        policy.expire_key_after = Duration::from_secs(0); // Force key rotation on every encrypt
        let policy = Arc::new(policy);
        let crypto = Arc::new(Aes256GcmAead::new());
        let secret_factory = Arc::new(DefaultSecretFactory::new());
        let partition = Arc::new(DefaultPartition::new("test", "service", "product"));

        // Create caches
        let sk_cache = crate::key::cache::AnyCache::KeyCache(Arc::new(KeyCache::new(
            CacheKeyType::SystemKeys,
            policy.clone(),
        )));
        let ik_cache = crate::key::cache::AnyCache::KeyCache(Arc::new(KeyCache::new(
            CacheKeyType::IntermediateKeys,
            policy.clone(),
        )));

        // Create envelope encryption
        let encryption = EnvelopeEncryption::new(
            partition,
            metastore.clone(),
            kms.clone(),
            policy.clone(),
            crypto.clone(),
            secret_factory.clone(),
            sk_cache,
            ik_cache,
        );

        // Test data
        let data = b"hello world";

        // Encrypt data
        let drr1 = encryption.encrypt_payload(data).await?;

        // Delay to ensure different timestamp (timestamps are in seconds)
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;

        // Encrypt again to force key rotation
        let drr2 = encryption.encrypt_payload(data).await?;

        // Decrypt both records
        let decrypted1 = encryption.decrypt_data_row_record(&drr1).await?;
        let decrypted2 = encryption.decrypt_data_row_record(&drr2).await?;

        // Verify
        assert_eq!(data, decrypted1.as_slice());
        assert_eq!(data, decrypted2.as_slice());
        assert_ne!(drr1.key.created, drr2.key.created); // Keys should be different

        Ok(())
    }
}
