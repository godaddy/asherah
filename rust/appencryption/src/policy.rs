use chrono::{Duration, TimeZone, Utc};
use std::time;

/// Default values for CryptoPolicy
pub const DEFAULT_EXPIRE_AFTER: time::Duration = time::Duration::from_secs(60 * 60 * 24 * 90); // 90 days
pub const DEFAULT_REVOKED_CHECK_INTERVAL: time::Duration = time::Duration::from_secs(60 * 60); // 60 minutes
pub const DEFAULT_CREATE_DATE_PRECISION: time::Duration = time::Duration::from_secs(60); // 1 minute
pub const DEFAULT_KEY_CACHE_MAX_SIZE: usize = 1000;
pub const DEFAULT_SESSION_CACHE_MAX_SIZE: usize = 1000;
pub const DEFAULT_SESSION_CACHE_DURATION: time::Duration = time::Duration::from_secs(60 * 60 * 2); // 2 hours

/// Policy for encryption key management
#[derive(Debug, Clone)]
pub struct CryptoPolicy {
    /// Time after which a key is considered expired
    pub expire_key_after: time::Duration,

    /// Interval to check for revoked keys in the cache
    pub revoke_check_interval: time::Duration,

    /// Precision to use when creating new key timestamps
    pub create_date_precision: time::Duration,

    /// Whether to cache intermediate keys
    pub cache_intermediate_keys: bool,

    /// Maximum size of intermediate key cache
    pub intermediate_key_cache_max_size: usize,

    /// Eviction policy for intermediate key cache
    pub intermediate_key_cache_eviction_policy: String,

    /// Whether to share the intermediate key cache across sessions
    pub shared_intermediate_key_cache: bool,

    /// Whether to cache system keys
    pub cache_system_keys: bool,

    /// Maximum size of system key cache
    pub system_key_cache_max_size: usize,

    /// Eviction policy for system key cache
    pub system_key_cache_eviction_policy: String,

    /// Whether to cache sessions
    pub cache_sessions: bool,

    /// Maximum size of session cache
    pub session_cache_max_size: usize,

    /// Time before which keys should be considered revoked
    pub revoked_before: i64,

    /// How long to keep sessions in the cache
    pub session_cache_duration: time::Duration,

    /// Eviction policy for session cache
    pub session_cache_eviction_policy: String,
}

impl Default for CryptoPolicy {
    fn default() -> Self {
        Self {
            expire_key_after: DEFAULT_EXPIRE_AFTER,
            revoke_check_interval: DEFAULT_REVOKED_CHECK_INTERVAL,
            create_date_precision: DEFAULT_CREATE_DATE_PRECISION,
            cache_intermediate_keys: true,
            intermediate_key_cache_max_size: DEFAULT_KEY_CACHE_MAX_SIZE,
            intermediate_key_cache_eviction_policy: "simple".to_string(),
            shared_intermediate_key_cache: false,
            cache_system_keys: true,
            system_key_cache_max_size: DEFAULT_KEY_CACHE_MAX_SIZE,
            system_key_cache_eviction_policy: "simple".to_string(),
            cache_sessions: false,
            session_cache_max_size: DEFAULT_SESSION_CACHE_MAX_SIZE,
            session_cache_duration: DEFAULT_SESSION_CACHE_DURATION,
            session_cache_eviction_policy: "slru".to_string(),
            revoked_before: 0,
        }
    }
}

impl CryptoPolicy {
    /// Creates a new CryptoPolicy with the given options
    pub fn new() -> Self {
        Self::default()
    }

    /// Sets the system key cache eviction policy
    pub fn with_system_key_cache_policy(mut self, policy_name: impl Into<String>) -> Self {
        self.system_key_cache_eviction_policy = policy_name.into();
        self
    }

    /// Sets the intermediate key cache eviction policy
    pub fn with_intermediate_key_cache_policy(mut self, policy_name: impl Into<String>) -> Self {
        self.intermediate_key_cache_eviction_policy = policy_name.into();
        self
    }

    /// Sets the session cache eviction policy
    pub fn with_session_cache_policy(mut self, policy_name: impl Into<String>) -> Self {
        self.session_cache_eviction_policy = policy_name.into();
        self
    }

    /// Sets the expire after duration
    /// 
    /// This defines how long encryption keys remain valid before they're considered expired.
    /// Default is 90 days.
    /// 
    /// # Example
    /// ```
    /// use appencryption::policy::CryptoPolicy;
    /// use std::time::Duration;
    /// 
    /// let policy = CryptoPolicy::new()
    ///     .with_expire_after(Duration::from_secs(60 * 60 * 24 * 30)); // 30 days
    /// ```
    pub fn with_expire_after(mut self, duration: time::Duration) -> Self {
        self.expire_key_after = duration;
        self
    }

    /// Sets the revoke check interval
    /// 
    /// This defines how often to check for revoked keys in the cache.
    /// Default is 60 minutes.
    /// 
    /// # Example
    /// ```
    /// use appencryption::policy::CryptoPolicy;
    /// use std::time::Duration;
    /// 
    /// let policy = CryptoPolicy::new()
    ///     .with_revoke_check_interval(Duration::from_secs(60 * 30)); // 30 minutes
    /// ```
    pub fn with_revoke_check_interval(mut self, duration: time::Duration) -> Self {
        self.revoke_check_interval = duration;
        self
    }

    /// Disables caching of both system and intermediate keys
    pub fn with_no_cache(mut self) -> Self {
        self.cache_system_keys = false;
        self.cache_intermediate_keys = false;
        self
    }

    /// Enables a shared cache for intermediate keys
    pub fn with_shared_intermediate_key_cache(mut self, capacity: usize) -> Self {
        self.shared_intermediate_key_cache = true;
        self.intermediate_key_cache_max_size = capacity;
        self
    }

    /// Enables session caching
    pub fn with_session_cache(mut self) -> Self {
        self.cache_sessions = true;
        self
    }

    /// Sets the session cache max size
    pub fn with_session_cache_max_size(mut self, size: usize) -> Self {
        self.session_cache_max_size = size;
        self
    }

    /// Sets the session cache duration
    /// 
    /// This controls how long sessions remain in the cache before being evicted.
    /// Default is 2 hours.
    /// 
    /// # Example
    /// ```
    /// use appencryption::policy::CryptoPolicy;
    /// use std::time::Duration;
    /// 
    /// let policy = CryptoPolicy::new()
    ///     .with_session_cache_duration(Duration::from_secs(60 * 60)); // 1 hour
    /// ```
    pub fn with_session_cache_duration(mut self, duration: time::Duration) -> Self {
        self.session_cache_duration = duration;
        self
    }

    /// Sets the create date precision for new key timestamps
    /// 
    /// This controls the granularity of timestamps for new keys, which affects
    /// how many unique keys are created over time.
    /// Default is 1 minute.
    /// 
    /// # Example
    /// ```
    /// use appencryption::policy::CryptoPolicy;
    /// use std::time::Duration;
    /// 
    /// let policy = CryptoPolicy::new()
    ///     .with_create_date_precision(Duration::from_secs(60 * 5)); // 5 minutes
    /// ```
    pub fn with_create_date_precision(mut self, duration: time::Duration) -> Self {
        self.create_date_precision = duration;
        self
    }
}

/// Returns a new timestamp truncated to the given precision
pub fn new_key_timestamp(truncate: time::Duration) -> i64 {
    if truncate.as_secs() > 0 {
        let now = Utc::now();
        let timestamp = now.timestamp();
        let nanos = now.timestamp_subsec_nanos() as i64;
        let truncate_nanos = truncate.as_nanos() as i64;
        let remainder = (timestamp * 1_000_000_000 + nanos) % truncate_nanos;
        let truncated_nanos = timestamp * 1_000_000_000 + nanos - remainder;
        truncated_nanos / 1_000_000_000
    } else {
        Utc::now().timestamp()
    }
}

/// Checks if a key with the given creation timestamp is expired
pub fn is_key_expired(created: i64, expire_after: time::Duration) -> bool {
    if expire_after.as_secs() == 0 {
        return false;
    }

    let created_datetime = match Utc.timestamp_opt(created, 0) {
        chrono::LocalResult::Single(dt) => dt,
        _ => Utc::now(),
    };
    let expires_at = created_datetime + Duration::from_std(expire_after).unwrap_or_default();

    Utc::now() > expires_at
}

/// Configuration for the application encryption library
#[derive(Debug, Clone)]
pub struct Config {
    /// Service identifier
    pub service: String,

    /// Product identifier
    pub product: String,

    /// Crypto policy
    pub policy: CryptoPolicy,
}

impl Config {
    /// Creates a new Config
    pub fn new(
        service: impl Into<String>,
        product: impl Into<String>,
        policy: CryptoPolicy,
    ) -> Self {
        Self {
            service: service.into(),
            product: product.into(),
            policy,
        }
    }
}
