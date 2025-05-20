//! Tests for the AWS KMS implementation
//!
//! These tests validate the AWS KMS implementation using mocks.

use super::*;
use crate::crypto::aes256gcm::Aes256Gcm;
use std::collections::HashMap;
use async_trait::async_trait;
use std::sync::Arc;

#[derive(Debug)]
struct MockAwsKmsClient {
    region: String,
    should_fail: bool,
}

impl MockAwsKmsClient {
    fn new(region: String) -> Self {
        Self {
            region,
            should_fail: false,
        }
    }

    fn with_failure(region: String) -> Self {
        Self {
            region,
            should_fail: true,
        }
    }
}

#[async_trait]
impl AwsKmsClient for MockAwsKmsClient {
    async fn encrypt(&self, _key_id: &str, plaintext: &[u8]) -> Result<Vec<u8>> {
        if self.should_fail {
            Err(Error::Kms("Mock encryption failure".into()))
        } else {
            Ok(plaintext.to_vec())
        }
    }

    async fn decrypt(&self, ciphertext: &[u8]) -> Result<Vec<u8>> {
        if self.should_fail {
            Err(Error::Kms("Mock decryption failure".into()))
        } else {
            Ok(ciphertext.to_vec())
        }
    }

    async fn generate_data_key(&self, key_id: &str) -> Result<GenerateDataKeyResponse> {
        if self.should_fail {
            Err(Error::Kms("Mock generate data key failure".into()))
        } else {
            Ok(GenerateDataKeyResponse {
                key_id: key_id.to_string(),
                ciphertext_blob: vec![1, 2, 3, 4],
                plaintext: vec![5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
            })
        }
    }

    fn region(&self) -> &str {
        &self.region
    }
}

struct MockAead;

impl Aead for MockAead {
    fn encrypt(&self, data: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        // Simple mock implementation - just append the key to the data
        let mut result = data.to_vec();
        result.extend_from_slice(key);
        Ok(result)
    }

    fn decrypt(&self, ciphertext: &[u8], key: &[u8]) -> Result<Vec<u8>> {
        // Simple mock implementation - remove the key from the end of the ciphertext
        if ciphertext.len() < key.len() {
            return Err(Error::Crypto("Ciphertext too short".into()));
        }

        let data_len = ciphertext.len() - key.len();
        Ok(ciphertext[..data_len].to_vec())
    }
}

#[tokio::test]
async fn test_aws_kms_encrypt_decrypt() {
    // Create a mock client that won't fail
    let client = Arc::new(MockAwsKmsClient::new("us-west-2".to_string()));

    // Create a regional client
    let regional_client = RegionalClient::new(
        client,
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    // Create the KMS service
    let kms = AwsKms::new(
        vec![regional_client],
        Arc::new(MockAead),
    );

    // Test data
    let original_data = b"test data";

    // Encrypt the data
    let encrypted = kms.encrypt_key(original_data).await.unwrap();

    // Decrypt the data
    let decrypted = kms.decrypt_key(&encrypted).await.unwrap();

    // Verify the result
    assert_eq!(original_data, &decrypted[..]);
}

#[tokio::test]
async fn test_aws_kms_from_region_map() {
    // Create a region map
    let mut region_map = HashMap::new();
    region_map.insert(
        "us-west-2".to_string(),
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );
    region_map.insert(
        "us-east-1".to_string(),
        "arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    // This would fail in a real implementation since we'd try to create actual AWS clients
    // But in our mocked version it will work
    let kms = AwsKms::from_region_map(
        region_map,
        "us-west-2",
        Arc::new(MockAead),
    ).unwrap();

    // Verify the preferred region
    assert_eq!("us-west-2", kms.preferred_region());

    // Verify we have the expected number of clients
    assert_eq!(2, kms.clients.len());
}

#[tokio::test]
async fn test_aws_kms_failover() {
    // Create clients, first one will fail
    let failing_client = Arc::new(MockAwsKmsClient::with_failure("us-west-2".to_string()));
    let working_client = Arc::new(MockAwsKmsClient::new("us-east-1".to_string()));

    // Create regional clients
    let regional_client1 = RegionalClient::new(
        failing_client,
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    let regional_client2 = RegionalClient::new(
        working_client,
        "arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    // Create the KMS service with failover
    let kms = AwsKms::new(
        vec![regional_client1, regional_client2],
        Arc::new(MockAead),
    );

    // Test data
    let original_data = b"test data";

    // Encrypt the data - should fail over to the second client
    let encrypted = kms.encrypt_key(original_data).await.unwrap();

    // Decrypt the data
    let decrypted = kms.decrypt_key(&encrypted).await.unwrap();

    // Verify the result
    assert_eq!(original_data, &decrypted[..]);
}

#[tokio::test]
async fn test_aws_kms_all_regions_fail() {
    // Create clients that will all fail
    let failing_client1 = Arc::new(MockAwsKmsClient::with_failure("us-west-2".to_string()));
    let failing_client2 = Arc::new(MockAwsKmsClient::with_failure("us-east-1".to_string()));

    // Create regional clients
    let regional_client1 = RegionalClient::new(
        failing_client1,
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    let regional_client2 = RegionalClient::new(
        failing_client2,
        "arn:aws:kms:us-east-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab".to_string(),
    );

    // Create the KMS service with failing clients
    let kms = AwsKms::new(
        vec![regional_client1, regional_client2],
        Arc::new(MockAead),
    );

    // Test data
    let original_data = b"test data";

    // Encrypt the data - should fail
    let result = kms.encrypt_key(original_data).await;
    assert!(result.is_err());

    // Verify the error message
    let err = result.unwrap_err();
    if let Error::Kms(msg) = err {
        assert!(msg.contains("All regions returned errors"));
    } else {
        panic!("Unexpected error type: {:?}", err);
    }
}