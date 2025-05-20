use crate::envelope::KeyMeta;
use crate::error::Result;
use crate::key::CryptoKey;
use crate::policy::CryptoPolicy;
use chrono::{DateTime, Duration, Utc};
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::sync::atomic::{AtomicI64, Ordering};

/// A cached CryptoKey with reference counting
pub struct CachedCryptoKey {
    /// The underlying CryptoKey
    pub crypto_key: Arc<CryptoKey>,
    
    /// Reference count for this key
    refs: AtomicI64,
}

impl CachedCryptoKey {
    /// Creates a new CachedCryptoKey with initial reference count of 1
    pub fn new(key: CryptoKey) -> Self {
        Self {
            crypto_key: Arc::new(key),
            refs: AtomicI64::new(1), // Initial reference count of 1 for the cache
        }
    }
    
    /// Closes the key if reference count reaches zero
    pub fn close(&self) -> Result<()> {
        if self.refs.fetch_sub(1, Ordering::AcqRel) > 1 {
            return Ok(());
        }

        log::debug!("Closing cached key: {:p}, refs={}", 
            self.crypto_key, self.refs.load(Ordering::Relaxed));
        
        // We can't actually close since we don't have mutable access
        // The actual key will be dropped when the Arc reference count reaches zero
        
        Ok(())
    }
    
    /// Increments the reference count
    pub fn increment(&self) {
        self.refs.fetch_add(1, Ordering::AcqRel);
    }
}

/// Entry in the key cache
#[derive(Clone)]
struct CacheEntry {
    /// Time when this entry was loaded
    loaded_at: DateTime<Utc>,
    
    /// The cached key
    key: Arc<CachedCryptoKey>,
}

impl CacheEntry {
    /// Creates a new CacheEntry with the current time
    fn new(key: CryptoKey) -> Self {
        Self {
            loaded_at: Utc::now(),
            key: Arc::new(CachedCryptoKey::new(key)),
        }
    }
}

/// Cache key type
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CacheKeyType {
    /// Cache for system keys
    SystemKeys,
    
    /// Cache for intermediate keys
    IntermediateKeys,
}

impl std::fmt::Display for CacheKeyType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            CacheKeyType::SystemKeys => write!(f, "system"),
            CacheKeyType::IntermediateKeys => write!(f, "intermediate"),
        }
    }
}

/// Functions to cache and retrieve keys
#[async_trait::async_trait]
pub trait KeyCacher: Send + Sync {
    /// Gets a key from the cache or loads it using the provided function
    async fn get_or_load<F, Fut>(&self, 
        meta: KeyMeta, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send;
    
    /// Gets the latest key from the cache or loads it using the provided function
    async fn get_or_load_latest<F, Fut>(&self, 
        id: &str, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send;
        
    /// Closes the cache
    async fn close(&self) -> Result<()>;
}

/// Format key for cache
fn cache_key(id: &str, created: i64) -> String {
    format!("{}{}", id, created)
}

/// Implements a cache with an LRU eviction policy
pub struct KeyCache {
    /// The crypto policy
    policy: Arc<CryptoPolicy>,
    
    /// The actual cache using string keys
    keys: RwLock<HashMap<String, CacheEntry>>,
    
    /// Map from ID to latest key metadata
    latest: RwLock<HashMap<String, KeyMeta>>,
    
    /// Type of keys stored in this cache
    cache_type: CacheKeyType,
}

/// Parse a string cache policy name to a CachePolicy enum
pub fn parse_cache_policy(policy_str: &str) -> Option<crate::cache::CachePolicy> {
    match policy_str.to_lowercase().as_str() {
        "lru" => Some(crate::cache::CachePolicy::LRU),
        "lfu" => Some(crate::cache::CachePolicy::LFU),
        "tlfu" => Some(crate::cache::CachePolicy::TLFU),
        "slru" => Some(crate::cache::CachePolicy::SLRU),
        "simple" => Some(crate::cache::CachePolicy::Simple),
        _ => None,
    }
}

impl KeyCache {
    /// Creates a new KeyCache with the given policy and cache type
    pub fn new(cache_type: CacheKeyType, policy: Arc<CryptoPolicy>) -> Self {
        Self {
            policy,
            keys: RwLock::new(HashMap::new()),
            latest: RwLock::new(HashMap::new()),
            cache_type,
        }
    }
    
    /// Checks if a key needs to be reloaded based on the check interval
    fn is_reload_required(&self, entry: &CacheEntry) -> bool {
        if entry.key.crypto_key.is_revoked() {
            // No need to reload a revoked key
            return false;
        }
        
        let check_interval = Duration::from_std(self.policy.revoke_check_interval).unwrap_or_default();
        entry.loaded_at + check_interval < Utc::now()
    }
    
    /// Gets a fresh key from the cache
    fn get_fresh(&self, meta: &KeyMeta) -> Option<Arc<CachedCryptoKey>> {
        let keys = self.keys.read().unwrap();
        
        // If looking for the latest, use the stored latest metadata
        let cache_key_str = if meta.is_latest() {
            let latest = self.latest.read().unwrap();
            if let Some(latest_meta) = latest.get(&meta.id) {
                cache_key(&latest_meta.id, latest_meta.created)
            } else {
                cache_key(&meta.id, meta.created)
            }
        } else {
            cache_key(&meta.id, meta.created)
        };
        
        // Get the entry
        if let Some(entry) = keys.get(&cache_key_str) {
            if !self.is_reload_required(entry) {
                let key = Arc::clone(&entry.key);
                return Some(key);
            } else {
                log::debug!("{} stale -- id: {}-{}", 
                    self.cache_type, meta.id, entry.key.crypto_key.created());
                return None;
            }
        }
        
        None
    }
    
    /// Gets the latest key metadata for an ID
    fn get_latest_key_meta(&self, id: &str) -> Option<KeyMeta> {
        let latest = self.latest.read().unwrap();
        latest.get(&cache_key(id, 0)).cloned()
    }
    
    /// Maps the latest key metadata to an ID
    fn map_latest_key_meta(&self, id: &str, latest: KeyMeta) {
        let mut latest_map = self.latest.write().unwrap();
        latest_map.insert(cache_key(id, 0), latest);
    }
    
    /// Reads an entry from the cache
    fn read(&self, meta: &KeyMeta) -> Option<CacheEntry> {
        let keys = self.keys.read().unwrap();
        
        let id = if meta.is_latest() {
            if let Some(latest) = self.get_latest_key_meta(&meta.id) {
                cache_key(&latest.id, latest.created)
            } else {
                cache_key(&meta.id, meta.created)
            }
        } else {
            cache_key(&meta.id, meta.created)
        };
        
        keys.get(&id).cloned()
    }
    
    /// Writes an entry to the cache
    fn write(&self, meta: KeyMeta, entry: CacheEntry) {
        let mut keys = self.keys.write().unwrap();
        
        if meta.is_latest() {
            let updated_meta = KeyMeta {
                id: meta.id.clone(),
                created: entry.key.crypto_key.created(),
            };
            
            self.map_latest_key_meta(&meta.id, updated_meta.clone());
        } else if let Some(latest) = self.get_latest_key_meta(&meta.id) {
            if latest.created < entry.key.crypto_key.created() {
                self.map_latest_key_meta(&meta.id, meta.clone());
            }
        }
        
        let id = cache_key(&meta.id, meta.created);
        
        if let Some(existing) = keys.get(&id) {
            log::debug!("{} update -> old: {:p}, new: {:p}, id: {}", 
                self.cache_type, existing.key, entry.key, id);
        }
        
        log::debug!("{} write -> key: {:p}, id: {}", 
            self.cache_type, entry.key, id);
            
        keys.insert(id, entry);
    }
    
    /// Checks if a key is invalid (revoked or expired)
    fn is_invalid(&self, key: &Arc<CryptoKey>) -> bool {
        key.is_revoked() || 
        crate::policy::is_key_expired(key.created(), self.policy.expire_key_after)
    }
}

#[async_trait::async_trait]
impl KeyCacher for KeyCache {
    async fn get_or_load<F, Fut>(&self, 
        meta: KeyMeta, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        // Try to get a fresh key first with a read lock
        if let Some(key) = self.get_fresh(&meta) {
            key.increment();
            return Ok(key);
        }
        
        // If not found or stale, we need to load it
        let key = loader(meta.clone()).await?;
        
        // Check if we already have an entry
        if let Some(entry) = self.read(&meta) {
            // Update revocation status and last loaded time
            entry.key.crypto_key.set_revoked(key.is_revoked());
            
            // Return the cached entry
            entry.key.increment();
            return Ok(entry.key);
        }
        
        // Create a new entry
        let entry = CacheEntry::new(key);
        let result = Arc::clone(&entry.key);
        
        // Store in cache
        self.write(meta.clone(), entry);
        
        // Update latest if this was a latest request
        if meta.is_latest() {
            let mut latest = self.latest.write().unwrap();
            latest.insert(meta.id.clone(), KeyMeta {
                id: meta.id,
                created: result.crypto_key.created(),
            });
        }
        
        // Increment reference count for the caller
        result.increment();
        
        Ok(result)
    }
    
    async fn get_or_load_latest<F, Fut>(&self, 
        id: &str, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        let meta = KeyMeta {
            id: id.to_string(),
            created: 0, // Latest
        };
        
        // Try to get a fresh key
        if let Some(key) = self.get_fresh(&meta) {
            if !self.is_invalid(&key.crypto_key) {
                key.increment();
                return Ok(key);
            }
        }
        
        // Load the key
        let key = loader(meta.clone()).await?;
        
        // Create a new entry and return it
        let entry = CacheEntry::new(key);
        let new_meta = KeyMeta {
            id: id.to_string(),
            created: entry.key.crypto_key.created(),
        };
        
        // Update the latest mapping
        {
            let mut latest = self.latest.write().unwrap();
            latest.insert(id.to_string(), new_meta.clone());
        }
        
        let result = Arc::clone(&entry.key);
        self.write(new_meta, entry);
        
        // Increment reference count for the caller
        result.increment();
        
        Ok(result)
    }
    
    async fn close(&self) -> Result<()> {
        log::debug!("{} closing", self.cache_type);
        
        let mut keys = self.keys.write().unwrap();
        
        for (_, entry) in keys.drain() {
            entry.key.close()?;
        }
        
        Ok(())
    }
}

/// A cache implementation that never caches, always loads
pub struct NeverCache;

/// Wrapper enum for different cache implementations
#[derive(Clone)]
pub enum AnyCache {
    KeyCache(Arc<KeyCache>),
    NeverCache(Arc<NeverCache>),
}

#[async_trait::async_trait]
impl KeyCacher for AnyCache {
    async fn get_or_load<F, Fut>(&self, 
        meta: KeyMeta, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        match self {
            AnyCache::KeyCache(cache) => cache.get_or_load(meta, loader).await,
            AnyCache::NeverCache(cache) => cache.get_or_load(meta, loader).await,
        }
    }
    
    async fn get_or_load_latest<F, Fut>(&self, 
        id: &str, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        match self {
            AnyCache::KeyCache(cache) => cache.get_or_load_latest(id, loader).await,
            AnyCache::NeverCache(cache) => cache.get_or_load_latest(id, loader).await,
        }
    }
    
    async fn close(&self) -> Result<()> {
        match self {
            AnyCache::KeyCache(cache) => cache.close().await,
            AnyCache::NeverCache(_) => Ok(()),
        }
    }
}

#[async_trait::async_trait]
impl KeyCacher for NeverCache {
    async fn get_or_load<F, Fut>(&self, 
        meta: KeyMeta, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        let key = loader(meta).await?;
        Ok(Arc::new(CachedCryptoKey::new(key)))
    }
    
    async fn get_or_load_latest<F, Fut>(&self, 
        id: &str, 
        loader: F
    ) -> Result<Arc<CachedCryptoKey>>
    where
        F: FnOnce(KeyMeta) -> Fut + Send,
        Fut: std::future::Future<Output = Result<CryptoKey>> + Send
    {
        let meta = KeyMeta {
            id: id.to_string(),
            created: 0,
        };
        
        let key = loader(meta).await?;
        Ok(Arc::new(CachedCryptoKey::new(key)))
    }
    
    async fn close(&self) -> Result<()> {
        Ok(())
    }
}