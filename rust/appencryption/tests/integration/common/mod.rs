// Module for common test utilities and fixtures used across integration tests

use appencryption::{
    policy::CryptoPolicy,
    kms::StaticKeyManagementService,
    crypto::Aes256GcmAead,
    envelope::EnvelopeKeyRecord,
};
use std::sync::Arc;

// Constants for tests
pub const PRODUCT: &str = "product";
pub const SERVICE: &str = "service";
pub const PARTITION_ID: &str = "partition_1";
pub const ORIGINAL_DATA: &str = "somesupersecretstring!hjdkashfjkdashfd";
pub const STATIC_KEY: &str = "0000000000000000000000000000000000000000000000000000000000000000";

// Test configuration struct
#[derive(Clone)]
pub struct Config {
    pub product: String,
    pub service: String,
    pub policy: Arc<CryptoPolicy>,
}

// Create a standard test configuration
pub fn create_test_config() -> Config {
    Config {
        product: PRODUCT.to_string(),
        service: SERVICE.to_string(),
        policy: Arc::new(CryptoPolicy::new()),
    }
}

// Create the aes crypto implementation
pub fn create_crypto() -> Arc<Aes256GcmAead> {
    Arc::new(Aes256GcmAead::new())
}

// Create a static KMS for testing
pub async fn create_static_kms() -> Arc<StaticKeyManagementService> {
    // Convert the hex string to bytes
    let key_bytes = hex::decode(STATIC_KEY).expect("Invalid hex key");
    let static_kms = StaticKeyManagementService::new(key_bytes);
    
    Arc::new(static_kms)
}