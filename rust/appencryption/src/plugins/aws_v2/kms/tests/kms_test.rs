#[cfg(test)]
mod tests {
    use super::*;
    use crate::crypto::aes256gcm::Aes256Gcm;
    use crate::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
    use crate::KeyManagementService;
    use async_trait::async_trait;
    use std::collections::HashMap;
    use std::sync::Arc;

    // Mock KMS client for testing
    struct MockKmsClient {
        region: String,
        master_key: Vec<u8>,
    }

    impl MockKmsClient {
        fn new(region: impl Into<String>, master_key: Vec<u8>) -> Self {
            Self {
                region: region.into(),
                master_key,
            }
        }
    }

    #[async_trait]
    impl AwsKmsClient for MockKmsClient {
        async fn encrypt(&self, _key_id: &str, plaintext: &[u8]) -> Result<Vec<u8>> {
            // Simple XOR with master key for testing
            let mut result = vec![0u8; plaintext.len()];
            for (i, byte) in plaintext.iter().enumerate() {
                result[i] = byte ^ self.master_key[i % self.master_key.len()];
            }
            Ok(result)
        }

        async fn decrypt(&self, _key_id: &str, ciphertext: &[u8]) -> Result<Vec<u8>> {
            // Same XOR operation decrypts
            let mut result = vec![0u8; ciphertext.len()];
            for (i, byte) in ciphertext.iter().enumerate() {
                result[i] = byte ^ self.master_key[i % self.master_key.len()];
            }
            Ok(result)
        }

        async fn generate_data_key(&self, key_id: &str) -> Result<GenerateDataKeyResponse> {
            let plaintext = (0..32).map(|_| rand::random::<u8>()).collect::<Vec<u8>>();
            let ciphertext = self.encrypt(key_id, &plaintext).await?;

            Ok(GenerateDataKeyResponse {
                key_id: key_id.to_string(),
                ciphertext_blob: ciphertext,
                plaintext,
            })
        }

        fn region(&self) -> &str {
            &self.region
        }
    }

    #[tokio::test]
    async fn test_aws_kms_single_region() {
        // Create a mock KMS client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let client = Arc::new(MockKmsClient::new("us-west-2", master_key));

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_kms_client("us-west-2", client)
            .build()
            .expect("Failed to build AWS KMS");

        // Test encrypt/decrypt round trip
        let data = b"test data for encryption".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);
    }

    #[tokio::test]
    async fn test_aws_kms_multi_region() {
        // Create mock KMS clients
        let master_key1 = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let master_key2 = vec![16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];

        let client1 = Arc::new(MockKmsClient::new("us-west-2", master_key1));
        let client2 = Arc::new(MockKmsClient::new("us-east-1", master_key2));

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );
        arn_map.insert(
            "us-east-1".to_string(),
            "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with preferred region
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_preferred_region("us-west-2")
            .with_kms_client("us-west-2", client1)
            .with_kms_client("us-east-1", client2)
            .build()
            .expect("Failed to build AWS KMS");

        // Test encrypt/decrypt round trip
        let data = b"test data for multi-region encryption".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);
    }
}
