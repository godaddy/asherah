// Key management constants - same across all language implementations
pub const KEY_MANAGEMENT_STATIC_MASTER_KEY: &str = "thisIsAStaticMasterKeyForTesting";

// Common service configuration - same across all language implementations
pub const DEFAULT_SERVICE_ID: &str = "service";
pub const DEFAULT_PRODUCT_ID: &str = "product";
pub const DEFAULT_PARTITION_ID: &str = "partition";

// Crypto policy settings - explicit values to match other implementations
pub const KEY_EXPIRY_DAYS: u64 = 30;  // 30 days key expiry
pub const REVOKE_CHECK_MINUTES: u64 = 60;  // 60 minutes between revocation checks

// File output settings
pub const FILE_DIRECTORY: &str = "/tmp/";
pub const FILE_NAME: &str = "rust_encrypted";  // Used by other languages in decrypt tests