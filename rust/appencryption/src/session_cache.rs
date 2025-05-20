//! Session caching implementation for the application encryption library
//!
//! This module provides a session cache for reusing sessions across multiple requests,
//! with thread-safe session sharing and automatic cleanup.

use crate::cache::{Cache, CacheBuilder, CachePolicy};
use crate::error::Result;
use crate::session::EnvelopeSession;
use crate::Encryption;

use async_trait::async_trait;
use std::fmt;
use std::sync::{Arc, Condvar, Mutex, RwLock};
use std::time::{Duration, Instant};

/// Interface for session caching
pub trait SessionCache: Send + Sync {
    /// Get a session for the given partition ID
    fn get(&self, id: &str) -> Result<Arc<EnvelopeSession>>;

    /// Returns the number of sessions in the cache
    fn count(&self) -> usize;

    /// Close the session cache and all sessions
    fn close(&self);
}

/// Shared encryption wrapper to track concurrent session usage
pub struct SharedEncryption {
    /// Inner encryption implementation
    inner: Arc<dyn Encryption>,

    /// Creation time
    created: Instant,

    /// Access counter for reference tracking
    access_counter: Mutex<usize>,

    /// Condition variable for waiting until session is unused
    cond: Condvar,
}

impl SharedEncryption {
    /// Create a new shared encryption wrapper
    pub fn new(encryption: Arc<dyn Encryption>) -> Self {
        Self {
            inner: encryption,
            created: Instant::now(),
            access_counter: Mutex::new(0),
            cond: Condvar::new(),
        }
    }

    /// Increment the usage counter
    pub fn increment_usage(&self) {
        let mut counter = self.access_counter.lock().unwrap();
        *counter += 1;
    }

    /// Remove the session, waiting until all users are done
    pub fn remove(&self) {
        let mut counter = self.access_counter.lock().unwrap();

        // Wait until no more users
        while *counter > 0 {
            counter = self.cond.wait(counter).unwrap();
        }

        // Close the underlying encryption
        let _ = futures::executor::block_on(self.inner.close());
    }
}

#[async_trait]
impl Encryption for SharedEncryption {
    async fn encrypt_payload(&self, data: &[u8]) -> Result<crate::envelope::DataRowRecord> {
        self.inner.encrypt_payload(data).await
    }

    async fn decrypt_data_row_record(
        &self,
        drr: &crate::envelope::DataRowRecord,
    ) -> Result<Vec<u8>> {
        self.inner.decrypt_data_row_record(drr).await
    }

    async fn close(&self) -> Result<()> {
        let mut counter = self.access_counter.lock().unwrap();
        *counter -= 1;

        // Notify waiters
        self.cond.notify_all();

        Ok(())
    }

    fn as_any(&self) -> &dyn std::any::Any {
        self
    }
}

impl fmt::Debug for SharedEncryption {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("SharedEncryption")
            .field("created", &self.created)
            .finish()
    }
}

// SharedEncryption already implements Any through auto-implementation since it's a struct

/// Session loader function type
type SessionLoaderFn = Arc<dyn Fn(&str) -> Result<Arc<EnvelopeSession>> + Send + Sync>;

/// Cache wrapper for session cache
pub struct CacheWrapper {
    /// Session loader function
    loader: SessionLoaderFn,

    /// Underlying cache
    cache: Arc<dyn Cache<String, Arc<EnvelopeSession>>>,

    /// Lock for cache operations
    lock: RwLock<()>,
}

impl CacheWrapper {
    /// Create a new cache wrapper
    pub fn new(
        loader: SessionLoaderFn,
        max_size: usize,
        _expiry: Option<Duration>,
        eviction_policy: CachePolicy,
    ) -> Self {
        // Create cache with eviction callback
        let cache = CacheBuilder::<String, Arc<EnvelopeSession>>::new(max_size)
            .with_policy(eviction_policy)
            .with_evict_callback(|_, session| {
                // When evicted, we need to remove the session
                // No need for thread as these operations should be quick
                if let Some(shared_enc) = session
                    .encryption
                    .as_any()
                    .downcast_ref::<SharedEncryption>()
                {
                    shared_enc.remove();
                }
            })
            .build();

        // TODO: TTL is not implemented on the cache trait yet
        // if let Some(ttl) = expiry {
        //     cache.set_ttl(ttl);
        // }

        Self {
            loader,
            cache,
            lock: RwLock::new(()),
        }
    }

    /// Get or add a session to the cache
    fn get_or_add(&self, id: &str) -> Result<Arc<EnvelopeSession>> {
        // Try to get from cache first
        if let Some(session) = self.cache.get(&id.to_string()) {
            return Ok((*session).clone());
        }

        // Not in cache, create new session
        let session = (self.loader)(id)?;

        // Wrap in SharedEncryption if needed
        let session = self.ensure_shared(session);

        // Add to cache
        self.cache.insert(id.to_string(), session.clone());

        Ok(session)
    }

    /// Ensure the session uses SharedEncryption
    fn ensure_shared(&self, session: Arc<EnvelopeSession>) -> Arc<EnvelopeSession> {
        // Check if already using SharedEncryption
        if session
            .encryption
            .as_any()
            .downcast_ref::<SharedEncryption>()
            .is_none()
        {
            // Wrap in SharedEncryption
            let shared = Arc::new(SharedEncryption::new(session.encryption.clone()));

            // Create new session with shared encryption
            let new_session = Arc::new(EnvelopeSession::new(shared));
            return new_session;
        }

        // Already using SharedEncryption
        session
    }
}

impl SessionCache for CacheWrapper {
    fn get(&self, id: &str) -> Result<Arc<EnvelopeSession>> {
        let _guard = self.lock.write().unwrap();

        let session = self.get_or_add(id)?;

        // Increment usage counter
        if let Some(shared) = session
            .encryption
            .as_any()
            .downcast_ref::<SharedEncryption>()
        {
            shared.increment_usage();
        }

        Ok(session)
    }

    fn count(&self) -> usize {
        self.cache.len()
    }

    fn close(&self) {
        self.cache.clear();
    }
}

/// Create a new session cache with default configuration
pub fn new_session_cache(
    loader: impl Fn(&str) -> Result<Arc<EnvelopeSession>> + Send + Sync + 'static,
    max_size: usize,
    expiry: Option<Duration>,
    eviction_policy: Option<CachePolicy>,
) -> Arc<dyn SessionCache> {
    let policy = eviction_policy.unwrap_or(CachePolicy::LRU);
    let loader_fn = Arc::new(loader);

    Arc::new(CacheWrapper::new(loader_fn, max_size, expiry, policy))
}
