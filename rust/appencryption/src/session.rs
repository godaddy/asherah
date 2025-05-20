use crate::Encryption;
use crate::KeyManagementService;
use crate::Metastore;
use crate::Aead;
use crate::Loader;
use crate::Storer;
use crate::envelope::DataRowRecord;
use crate::error::{Error, Result};
use crate::policy::CryptoPolicy;
use crate::envelope::encryption::EnvelopeEncryption;
use crate::crypto::Aes256GcmAead;
use crate::partition::{DefaultPartition, SuffixedPartition};
use crate::key::cache::KeyCacher;

use async_trait::async_trait;
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

/// Session for performing encryption/decryption operations
#[async_trait]
pub trait Session: Send + Sync {
    /// Encrypts a payload
    async fn encrypt(&self, data: &[u8]) -> Result<DataRowRecord>;
    
    /// Decrypts a data row record
    async fn decrypt(&self, drr: &DataRowRecord) -> Result<Vec<u8>>;
    
    /// Stores data using a storer
    async fn store<S: Storer + 'static>(&self, data: &[u8], storer: S) -> Result<S::Key>;
    
    /// Loads data using a loader
    async fn load<L: Loader + 'static>(&self, key: &L::Key, loader: L) -> Result<Vec<u8>>;
    
    /// Closes the session
    async fn close(&self) -> Result<()>;
    
}

/// A function that configures a SessionFactory
pub type FactoryOption = Box<dyn Fn(&mut SessionFactory) + Send + Sync>;

/// A session factory for creating encryption sessions
pub struct SessionFactory {
    /// Service identifier
    service: String,
    
    /// Product identifier
    product: String,
    
    /// Crypto policy
    policy: Arc<CryptoPolicy>,
    
    /// Key management service
    kms: Arc<dyn KeyManagementService>,
    
    /// Metastore
    metastore: Arc<dyn Metastore>,
    
    /// AEAD implementation
    crypto: Arc<dyn Aead>,
    
    /// Secret factory
    secret_factory: Arc<DefaultSecretFactory>,
    
    /// Session cache
    session_cache: Option<Arc<dyn crate::session_cache::SessionCache>>,
    
    /// System key cache
    system_keys: crate::key::cache::AnyCache,
    
    /// Intermediate key cache (for shared caching)
    intermediate_keys: Option<crate::key::cache::AnyCache>,
}

impl SessionFactory {
    /// Creates a new SessionFactory
    pub fn new(
        service: impl Into<String>,
        product: impl Into<String>,
        policy: CryptoPolicy,
        kms: Arc<dyn KeyManagementService>,
        metastore: Arc<dyn Metastore>,
        secret_factory: Arc<DefaultSecretFactory>,
        opts: Vec<FactoryOption>,
    ) -> Self {
        let service_str = service.into();
        let product_str = product.into();
        // Create system key cache
        let system_keys = if policy.cache_system_keys {
            crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                crate::key::cache::CacheKeyType::SystemKeys,
                Arc::new(policy.clone()),
            )))
        } else {
            crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
        };
        
        // Create shared intermediate key cache if enabled
        let intermediate_keys = if policy.shared_intermediate_key_cache {
            Some(crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                crate::key::cache::CacheKeyType::IntermediateKeys,
                Arc::new(policy.clone()),
            ))))
        } else {
            None
        };
        
        // Create session cache if enabled
        let policy_arc = Arc::new(policy.clone());
        let session_cache = if policy.cache_sessions {
            let max_size = policy.session_cache_max_size;
            let expiry = if policy.session_cache_duration.as_secs() > 0 {
                Some(policy.session_cache_duration)
            } else {
                None
            };
            
            // Define the eviction policy
            let eviction_policy = match policy.session_cache_eviction_policy.as_str() {
                "lru" => Some(crate::cache::CachePolicy::LRU),
                "lfu" => Some(crate::cache::CachePolicy::LFU),
                "tlfu" => Some(crate::cache::CachePolicy::TLFU),
                "slru" => Some(crate::cache::CachePolicy::LRU), // SLRU not implemented yet, use LRU as default
                "" => Some(crate::cache::CachePolicy::LRU),
                _ => Some(crate::cache::CachePolicy::LRU),
            };
            
            // Define the session loader
            let factory_service = service_str.clone();
            let factory_product = product_str.clone();
            let factory_kms = kms.clone();
            let factory_metastore = metastore.clone();
            let factory_crypto = Arc::new(Aes256GcmAead::new());
            let factory_secret_factory = secret_factory.clone();
            let factory_system_keys = system_keys.clone();
            let factory_intermediate_keys = intermediate_keys.clone();
            let factory_policy = policy_arc.clone();
            
            let loader = move |id: &str| -> Result<Arc<EnvelopeSession>> {
                let partition = Arc::new(DefaultPartition::new(
                    id,
                    &factory_service,
                    &factory_product,
                ));
                
                // Choose the intermediate key cache
                let ik_cache = if let Some(shared_ik_cache) = &factory_intermediate_keys {
                    shared_ik_cache.clone()
                } else if factory_policy.cache_intermediate_keys {
                    crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                        crate::key::cache::CacheKeyType::IntermediateKeys,
                        factory_policy.clone(),
                    )))
                } else {
                    crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
                };
                
                // Create encryption
                let encryption = Arc::new(
                    crate::envelope::encryption::EnvelopeEncryption::builder()
                        .with_partition(partition)
                        .with_metastore(factory_metastore.clone())
                        .with_kms(factory_kms.clone())
                        .with_policy(factory_policy.clone())
                        .with_crypto(factory_crypto.clone())
                        .with_secret_factory(factory_secret_factory.clone())
                        .with_sk_cache(factory_system_keys.clone())
                        .with_ik_cache(ik_cache)
                        .build()?
                );
                
                // Create session
                let session = Arc::new(EnvelopeSession::new(encryption));
                Ok(session)
            };
            
            Some(crate::session_cache::new_session_cache(
                loader,
                max_size,
                expiry,
                eviction_policy,
            ))
        } else {
            None
        };
        
        // Register key metrics
        if crate::metrics::metrics_enabled() {
            crate::metrics::register_counter("ael.sessions.created");
            crate::metrics::register_counter("ael.sessions.cached");
            crate::metrics::register_counter("ael.sessions.cache_hits");
            crate::metrics::register_counter("ael.sessions.cache_misses");
            crate::metrics::register_gauge("ael.sessions.active");
            crate::metrics::register_timer("ael.sessions.encrypt.time");
            crate::metrics::register_timer("ael.sessions.decrypt.time");
        }
        
        let mut factory = Self {
            service: service_str.clone(),
            product: product_str.clone(),
            policy: policy_arc,
            kms,
            metastore,
            crypto: Arc::new(Aes256GcmAead::new()),
            secret_factory,
            session_cache,
            system_keys,
            intermediate_keys,
        };
        
        // Apply options
        for opt in opts {
            opt(&mut factory);
        }
        
        factory
    }
    
    /// Creates a new SessionFactory with default options
    pub fn builder() -> SessionFactoryBuilder {
        SessionFactoryBuilder::new()
    }
    
    /// Creates a new session for a partition
    pub async fn session(&self, partition_id: impl Into<String>) -> Result<Arc<EnvelopeSession>> {
        let id = partition_id.into();
        
        if id.is_empty() {
            return Err(Error::InvalidArgument("partition id cannot be empty".to_string()));
        }
        
        // Check if we have a session cache
        if let Some(cache) = &self.session_cache {
            if crate::metrics::metrics_enabled() {
                crate::metrics::increment_counter("ael.sessions.cached", 1);
            }
            
            let session = cache.get(&id);
            
            if session.is_ok() && crate::metrics::metrics_enabled() {
                crate::metrics::increment_counter("ael.sessions.cache_hits", 1);
            } else if crate::metrics::metrics_enabled() {
                crate::metrics::increment_counter("ael.sessions.cache_misses", 1);
            }
            
            return session;
        }
        
        // No cache, create a new session
        if crate::metrics::metrics_enabled() {
            crate::metrics::increment_counter("ael.sessions.created", 1);
        }
        
        let partition = Arc::new(DefaultPartition::new(
            &id,
            &self.service,
            &self.product,
        ));
        
        // Choose the intermediate key cache
        let ik_cache = if let Some(shared_ik_cache) = &self.intermediate_keys {
            shared_ik_cache.clone()
        } else if self.policy.cache_intermediate_keys {
            crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                crate::key::cache::CacheKeyType::IntermediateKeys,
                self.policy.clone(),
            )))
        } else {
            crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
        };
        
        let encryption = EnvelopeEncryption::builder()
            .with_partition(partition)
            .with_metastore(self.metastore.clone())
            .with_kms(self.kms.clone())
            .with_policy(self.policy.clone())
            .with_crypto(self.crypto.clone())
            .with_secret_factory(self.secret_factory.clone())
            .with_sk_cache(self.system_keys.clone())
            .with_ik_cache(ik_cache)
            .build()?;
        
        let session = Arc::new(EnvelopeSession::new(Arc::new(encryption)));
        
        // Update active sessions gauge
        if crate::metrics::metrics_enabled() {
            let count = if let Some(cache) = &self.session_cache {
                cache.count() as f64
            } else {
                1.0
            };
            crate::metrics::record_gauge("ael.sessions.active", count);
        }
        
        Ok(session)
    }
    
    /// Creates a new session with a suffixed partition
    pub async fn session_with_suffix(
        &self,
        partition_id: impl Into<String>,
        suffix: impl Into<String>,
    ) -> Result<Arc<EnvelopeSession>> {
        let id = partition_id.into();
        
        if id.is_empty() {
            return Err(Error::InvalidArgument("partition id cannot be empty".to_string()));
        }
        
        // For suffixed partitions, we don't use the cache as the ID gets modified
        let partition = Arc::new(SuffixedPartition::new(
            &id,
            &self.service,
            &self.product,
            suffix,
        ));
        
        // Choose the intermediate key cache
        let ik_cache = if let Some(shared_ik_cache) = &self.intermediate_keys {
            shared_ik_cache.clone()
        } else if self.policy.cache_intermediate_keys {
            crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                crate::key::cache::CacheKeyType::IntermediateKeys,
                self.policy.clone(),
            )))
        } else {
            crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
        };
        
        let encryption = EnvelopeEncryption::builder()
            .with_partition(partition)
            .with_metastore(self.metastore.clone())
            .with_kms(self.kms.clone())
            .with_policy(self.policy.clone())
            .with_crypto(self.crypto.clone())
            .with_secret_factory(self.secret_factory.clone())
            .with_sk_cache(self.system_keys.clone())
            .with_ik_cache(ik_cache)
            .build()?;
        
        Ok(Arc::new(EnvelopeSession::new(Arc::new(encryption))))
    }
    
    /// Close all resources held by the session factory
    pub async fn close(&self) -> Result<()> {
        // Close session cache if present
        if let Some(cache) = &self.session_cache {
            cache.close();
        }
        
        // Close shared intermediate key cache if present
        if let Some(ik_cache) = &self.intermediate_keys {
            ik_cache.close().await?;
        }
        
        // Close system key cache
        self.system_keys.close().await?;
        
        Ok(())
    }
}

/// Factory options for configuring SessionFactory
pub struct SessionFactoryOptions {
    /// Whether to enable metrics
    pub enable_metrics: bool,
}

impl Default for SessionFactoryOptions {
    fn default() -> Self {
        Self {
            enable_metrics: true,
        }
    }
}

/// A builder for SessionFactory
#[derive(Default)]
pub struct SessionFactoryBuilder {
    service: Option<String>,
    product: Option<String>,
    policy: Option<CryptoPolicy>,
    kms: Option<Arc<dyn KeyManagementService>>,
    metastore: Option<Arc<dyn Metastore>>,
    crypto: Option<Arc<dyn Aead>>,
    secret_factory: Option<Arc<DefaultSecretFactory>>,
    options: Vec<FactoryOption>,
}


impl SessionFactoryBuilder {
    /// Creates a new SessionFactoryBuilder
    pub fn new() -> Self {
        Self::default()
    }
    
    /// Sets the service name
    pub fn with_service(mut self, service: impl Into<String>) -> Self {
        self.service = Some(service.into());
        self
    }
    
    /// Sets the product name
    pub fn with_product(mut self, product: impl Into<String>) -> Self {
        self.product = Some(product.into());
        self
    }
    
    /// Sets the crypto policy
    pub fn with_policy(mut self, policy: CryptoPolicy) -> Self {
        self.policy = Some(policy);
        self
    }
    
    /// Sets the key management service
    pub fn with_kms(mut self, kms: Arc<dyn KeyManagementService>) -> Self {
        self.kms = Some(kms);
        self
    }
    
    /// Sets the metastore
    pub fn with_metastore(mut self, metastore: Arc<dyn Metastore>) -> Self {
        self.metastore = Some(metastore);
        self
    }
    
    /// Sets the AEAD implementation
    pub fn with_crypto(mut self, crypto: Arc<dyn Aead>) -> Self {
        self.crypto = Some(crypto);
        self
    }
    
    /// Sets the secret factory
    pub fn with_secret_factory(mut self, secret_factory: Arc<DefaultSecretFactory>) -> Self {
        self.secret_factory = Some(secret_factory);
        self
    }
    
    /// Adds a factory option
    pub fn with_option(mut self, option: impl Fn(&mut SessionFactory) + Send + Sync + 'static) -> Self {
        self.options.push(Box::new(option));
        self
    }
    
    /// Sets whether metrics are enabled
    pub fn with_metrics(mut self, enabled: bool) -> Self {
        self.options.push(Box::new(move |_factory: &mut SessionFactory| {
            if !enabled {
                crate::metrics::disable_metrics();
            }
        }));
        self
    }
    
    /// Builds the SessionFactory
    pub fn build(self) -> Result<SessionFactory> {
        let service = self.service.ok_or_else(|| Error::InvalidArgument("service name is required".to_string()))?;
        let product = self.product.ok_or_else(|| Error::InvalidArgument("product name is required".to_string()))?;
        let policy = self.policy.ok_or_else(|| Error::InvalidArgument("crypto policy is required".to_string()))?;
        let kms = self.kms.ok_or_else(|| Error::InvalidArgument("key management service is required".to_string()))?;
        let metastore = self.metastore.ok_or_else(|| Error::InvalidArgument("metastore is required".to_string()))?;
        let secret_factory = self.secret_factory.ok_or_else(|| Error::InvalidArgument("secret factory is required".to_string()))?;
        
        let factory = if let Some(crypto) = self.crypto {
            // Create with custom crypto
            let mut f = SessionFactory {
                service,
                product,
                policy: Arc::new(policy.clone()),
                kms,
                metastore,
                crypto,
                secret_factory,
                session_cache: None,
                system_keys: crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache)),
                intermediate_keys: None,
            };
            
            // Initialize caches
            f.system_keys = if policy.cache_system_keys {
                crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                    crate::key::cache::CacheKeyType::SystemKeys,
                    Arc::new(policy.clone()),
                )))
            } else {
                crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
            };
            
            f.intermediate_keys = if policy.shared_intermediate_key_cache {
                Some(crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                    crate::key::cache::CacheKeyType::IntermediateKeys,
                    Arc::new(policy.clone()),
                ))))
            } else {
                None
            };
            
            // Initialize session cache
            if policy.cache_sessions {
                // Convert session cache policy string to enum
                let cache_policy = crate::key::cache::parse_cache_policy(&policy.session_cache_eviction_policy)
                    .unwrap_or(crate::cache::CachePolicy::LRU); // Default to LRU if unknown
                let max_size = policy.session_cache_max_size;
                let expiry = if policy.session_cache_duration.as_secs() > 0 {
                    Some(policy.session_cache_duration)
                } else {
                    None
                };
                
                // Define the eviction policy
                let eviction_policy = match policy.session_cache_eviction_policy.as_str() {
                    "lru" => Some(crate::cache::CachePolicy::LRU),
                    "lfu" => Some(crate::cache::CachePolicy::LFU),
                    "tlfu" => Some(crate::cache::CachePolicy::TLFU),
                    "slru" => Some(crate::cache::CachePolicy::LRU), // SLRU not implemented yet, use LRU as default
                    "" => Some(crate::cache::CachePolicy::LRU),
                    _ => Some(crate::cache::CachePolicy::LRU),
                };
                
                // Define the session loader
                let factory_service = f.service.clone();
                let factory_product = f.product.clone();
                let factory_kms = f.kms.clone();
                let factory_metastore = f.metastore.clone();
                let factory_crypto = f.crypto.clone();
                let factory_secret_factory = f.secret_factory.clone();
                let factory_system_keys = f.system_keys.clone();
                let factory_intermediate_keys = f.intermediate_keys.clone();
                let factory_policy = Arc::new(policy.clone());
                
                let loader = move |id: &str| -> Result<Arc<EnvelopeSession>> {
                    let partition = Arc::new(DefaultPartition::new(
                        id,
                        &factory_service,
                        &factory_product,
                    ));
                    
                    // Choose the intermediate key cache
                    let ik_cache = if let Some(shared_ik_cache) = &factory_intermediate_keys {
                        shared_ik_cache.clone()
                    } else if factory_policy.cache_intermediate_keys {
                        crate::key::cache::AnyCache::KeyCache(Arc::new(crate::key::cache::KeyCache::new(
                            crate::key::cache::CacheKeyType::IntermediateKeys,
                            factory_policy.clone(),
                        )))
                    } else {
                        crate::key::cache::AnyCache::NeverCache(Arc::new(crate::key::cache::NeverCache))
                    };
                    
                    // Create encryption
                    let encryption = Arc::new(
                        crate::envelope::encryption::EnvelopeEncryption::builder()
                            .with_partition(partition)
                            .with_metastore(factory_metastore.clone())
                            .with_kms(factory_kms.clone())
                            .with_policy(factory_policy.clone())
                            .with_crypto(factory_crypto.clone())
                            .with_secret_factory(factory_secret_factory.clone())
                            .with_sk_cache(factory_system_keys.clone())
                            .with_ik_cache(ik_cache)
                            .build()?
                    );
                    
                    // Create session
                    let session = Arc::new(EnvelopeSession::new(encryption));
                    Ok(session)
                };
                
                f.session_cache = Some(crate::session_cache::new_session_cache(
                    loader,
                    max_size,
                    expiry,
                    eviction_policy,
                ));
            }
            
            f
        } else {
            // Create with default crypto (AES-256-GCM)
            SessionFactory::new(
                service,
                product,
                policy,
                kms,
                metastore,
                secret_factory,
                self.options,
            )
        };
        
        // Register metrics if not disabled
        if crate::metrics::metrics_enabled() {
            crate::metrics::register_counter("ael.sessions.created");
            crate::metrics::register_counter("ael.sessions.cached");
            crate::metrics::register_counter("ael.sessions.cache_hits");
            crate::metrics::register_counter("ael.sessions.cache_misses");
            crate::metrics::register_gauge("ael.sessions.active");
            crate::metrics::register_timer("ael.sessions.encrypt.time");
            crate::metrics::register_timer("ael.sessions.decrypt.time");
        }
        
        Ok(factory)
    }
}

/// Creates a factory option to set the secret factory
pub fn with_secret_factory(factory: Arc<DefaultSecretFactory>) -> FactoryOption {
    Box::new(move |f: &mut SessionFactory| {
        f.secret_factory = factory.clone();
    })
}

/// Creates a factory option to set metrics
pub fn with_metrics(enabled: bool) -> FactoryOption {
    Box::new(move |_: &mut SessionFactory| {
        if !enabled {
            crate::metrics::disable_metrics();
        }
    })
}

/// Session implementation using envelope encryption
#[derive(Clone)]
pub struct EnvelopeSession {
    /// Encryption implementation
    pub(crate) encryption: Arc<dyn Encryption>,
}

impl EnvelopeSession {
    /// Creates a new EnvelopeSession
    pub fn new(encryption: Arc<dyn Encryption>) -> Self {
        Self { encryption }
    }
}

#[async_trait]
impl Session for EnvelopeSession {
    async fn encrypt(&self, data: &[u8]) -> Result<DataRowRecord> {
        let timer = crate::timer!("ael.sessions.encrypt.time");
        
        let result = self.encryption.encrypt_payload(data).await;
        
        if let Some(t) = timer {
            t.observe_duration();
        }
        
        result
    }
    
    async fn decrypt(&self, drr: &DataRowRecord) -> Result<Vec<u8>> {
        let timer = crate::timer!("ael.sessions.decrypt.time");
        
        let result = self.encryption.decrypt_data_row_record(drr).await;
        
        if let Some(t) = timer {
            t.observe_duration();
        }
        
        result
    }
    
    async fn store<S: Storer + 'static>(&self, data: &[u8], storer: S) -> Result<S::Key> {
        // Encrypt the data
        let drr = self.encrypt(data).await?;
        
        // Store the encrypted data
        storer.store(&drr).await
    }
    
    async fn load<L: Loader + 'static>(&self, key: &L::Key, loader: L) -> Result<Vec<u8>> {
        // Load the encrypted data
        let drr = loader.load(key).await?
            .ok_or_else(|| crate::error::Error::Internal("Data not found for key".to_string()))?;
            
        // Decrypt the data
        self.decrypt(&drr).await
    }
    
    async fn close(&self) -> Result<()> {
        self.encryption.close().await
    }
}