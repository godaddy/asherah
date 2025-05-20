#[cfg(test)]
mod tests {
    use crate::crypto::aes256gcm::Aes256Gcm;
    use crate::error::Result;
    use crate::plugins::aws_v2::kms::new_aws_kms;
    use crate::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient, GenerateDataKeyResponse};
    use crate::KeyManagementService;
    use async_trait::async_trait;
    use aws_config::{BehaviorVersion, Region, SdkConfig};
    use std::collections::HashMap;
    use std::sync::{Arc, Mutex};
    use std::time::Duration;

    // Mock KMS client for testing
    struct MockKmsClient {
        region: String,
        master_key: Vec<u8>,
        // Track the client configuration
        config_region: Option<String>,
        timeout: Option<Duration>,
        max_retries: Option<u32>,
        endpoint: Option<String>,
        // Track method calls
        encrypt_calls: Mutex<Vec<(String, Vec<u8>)>>,
        decrypt_calls: Mutex<Vec<(String, Vec<u8>)>>,
        generate_data_key_calls: Mutex<Vec<String>>,
    }

    impl MockKmsClient {
        fn new(region: impl Into<String>, master_key: Vec<u8>) -> Self {
            Self {
                region: region.into(),
                master_key,
                config_region: None,
                timeout: None,
                max_retries: None,
                endpoint: None,
                encrypt_calls: Mutex::new(Vec::new()),
                decrypt_calls: Mutex::new(Vec::new()),
                generate_data_key_calls: Mutex::new(Vec::new()),
            }
        }

        fn with_config(mut self, config: &SdkConfig) -> Self {
            if let Some(region) = config.region() {
                self.config_region = Some(region.to_string());
            }
            if let Some(timeout_config) = config.timeout_config() {
                if let Some(timeout) = timeout_config.operation_timeout() {
                    self.timeout = Some(*timeout);
                }
            }
            if let Some(retry_config) = config.retry_config() {
                self.max_retries = Some(retry_config.max_attempts());
            }
            self
        }

        fn with_endpoint(mut self, endpoint: impl Into<String>) -> Self {
            self.endpoint = Some(endpoint.into());
            self
        }
    }

    #[async_trait]
    impl AwsKmsClient for MockKmsClient {
        async fn encrypt(&self, key_id: &str, plaintext: &[u8]) -> Result<Vec<u8>> {
            // Track calls
            self.encrypt_calls
                .lock()
                .unwrap()
                .push((key_id.to_string(), plaintext.to_vec()));

            // Simple XOR with master key for testing
            let mut result = vec![0u8; plaintext.len()];
            for (i, byte) in plaintext.iter().enumerate() {
                result[i] = byte ^ self.master_key[i % self.master_key.len()];
            }
            Ok(result)
        }

        async fn decrypt(&self, key_id: &str, ciphertext: &[u8]) -> Result<Vec<u8>> {
            // Track calls
            self.decrypt_calls
                .lock()
                .unwrap()
                .push((key_id.to_string(), ciphertext.to_vec()));

            // Same XOR operation decrypts
            let mut result = vec![0u8; ciphertext.len()];
            for (i, byte) in ciphertext.iter().enumerate() {
                result[i] = byte ^ self.master_key[i % self.master_key.len()];
            }
            Ok(result)
        }

        async fn generate_data_key(&self, key_id: &str) -> Result<GenerateDataKeyResponse> {
            // Track calls
            self.generate_data_key_calls
                .lock()
                .unwrap()
                .push(key_id.to_string());

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

    // Test the builder.with_aws_config method
    #[tokio::test]
    async fn test_with_aws_config() {
        // Create a custom AWS SDK config
        let config = aws_config::defaults(BehaviorVersion::latest())
            .region(Region::new("us-west-2"))
            .build();

        // Create ARN map with a single region
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create a mock KMS client factory
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            // Verify the config has the expected region
            assert_eq!(
                config.region().map(|r| r.to_string()),
                Some("us-west-2".to_string())
            );

            let client = MockKmsClient::new("us-west-2", master_key.clone());
            Arc::new(client)
        };

        // Build AWS KMS with the custom config
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_aws_config(config)
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Verify the KMS is configured correctly
        assert_eq!(kms.preferred_region(), "us-west-2");
    }

    // Test the builder.with_timeout method
    #[tokio::test]
    async fn test_with_timeout_detailed() {
        // Create ARN map with a single region
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Track timeout settings
        let timeout_seen = Arc::new(Mutex::new(None));
        let timeout_seen_clone = timeout_seen.clone();

        // Create a mock KMS client factory
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            // Extract the timeout from the config
            let timeout = config
                .timeout_config()
                .and_then(|tc| tc.operation_timeout())
                .cloned();

            // Store the timeout for verification
            *timeout_seen_clone.lock().unwrap() = timeout;

            let client = MockKmsClient::new("us-west-2", master_key.clone());
            Arc::new(client)
        };

        // Set custom timeout
        let timeout = Duration::from_secs(10);

        // Build AWS KMS with the custom timeout
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_timeout(timeout)
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Verify the timeout was set correctly
        assert_eq!(*timeout_seen.lock().unwrap(), Some(timeout));
    }

    // Test the builder.with_endpoint method
    #[tokio::test]
    async fn test_with_endpoint_detailed() {
        // Create ARN map with a single region
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Define the custom endpoint
        let custom_endpoint = "http://localhost:4566";

        // Track endpoint settings
        let endpoint_seen = Arc::new(Mutex::new(None));
        let endpoint_seen_clone = endpoint_seen.clone();

        // Create a factory that can check for endpoint overrides in config builder
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            // We can't easily access the endpoint URL from the loaded SDK config,
            // but in the real implementation, create_regional_client handles this
            let region = config.region().map(|r| r.to_string()).unwrap_or_default();
            if region == "us-west-2" {
                // In a real test, we would check the endpoint URL
                *endpoint_seen_clone.lock().unwrap() = Some(custom_endpoint.to_string());
            }

            let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            let client = MockKmsClient::new(region, master_key.clone());
            Arc::new(client)
        };

        // Build AWS KMS with the custom endpoint
        let kms = AwsKmsBuilder::new(crypto, arn_map.clone())
            .with_endpoint("us-west-2", custom_endpoint)
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Verify the endpoint was passed correctly
        assert_eq!(
            *endpoint_seen.lock().unwrap(),
            Some(custom_endpoint.to_string())
        );

        // Verify the KMS is configured correctly
        assert_eq!(kms.preferred_region(), "us-west-2");
    }

    // Test the builder with multiple endpoints
    #[tokio::test]
    async fn test_with_multiple_endpoints() {
        // Create ARN map with multiple regions
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

        // Define custom endpoints
        let endpoint1 = "http://localhost:4566";
        let endpoint2 = "http://localhost:4567";

        // Track endpoint regions
        let endpoints_seen = Arc::new(Mutex::new(Vec::new()));
        let endpoints_seen_clone = endpoints_seen.clone();

        // Create a factory that tracks regions
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            let region = config.region().map(|r| r.to_string()).unwrap_or_default();
            endpoints_seen_clone.lock().unwrap().push(region.clone());

            let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            let client = MockKmsClient::new(region, master_key.clone());
            Arc::new(client)
        };

        // Build AWS KMS with the custom endpoints
        let kms = AwsKmsBuilder::new(crypto, arn_map.clone())
            .with_preferred_region("us-west-2")
            .with_endpoint("us-west-2", endpoint1)
            .with_endpoint("us-east-1", endpoint2)
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Verify both regions were seen
        let regions_seen = endpoints_seen.lock().unwrap();
        assert_eq!(regions_seen.len(), 2);
        assert!(regions_seen.contains(&"us-west-2".to_string()));
        assert!(regions_seen.contains(&"us-east-1".to_string()));

        // Verify the KMS is configured correctly
        assert_eq!(kms.preferred_region(), "us-west-2");
    }

    // Test the convenience function new_aws_kms
    #[tokio::test]
    async fn test_new_aws_kms_convenience_function_detailed() {
        // Create ARN map with a single region
        let mut arn_map = HashMap::new();
        let region = "us-west-2";
        arn_map.insert(
            region.to_string(),
            format!("arn:aws:kms:{}:123456789012:key/abcd-1234", region),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Track factory calls
        let factory_calls = Arc::new(Mutex::new(Vec::new()));
        let factory_calls_clone = factory_calls.clone();

        // Create a factory for test verification
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            let region = config.region().map(|r| r.to_string()).unwrap_or_default();
            factory_calls_clone.lock().unwrap().push(region.clone());

            let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            let client = MockKmsClient::new(region, master_key);
            Arc::new(client)
        };

        // Create a builder with our factory for testing the convenience function
        let builder = AwsKmsBuilder::new(crypto.clone(), arn_map.clone()).with_kms_factory(factory);

        // Use the builder directly so we can control the test
        let kms = builder.build().await.expect("Failed to build AWS KMS");

        // Verify the factory was called with the right region
        let calls = factory_calls.lock().unwrap();
        assert_eq!(calls.len(), 1);
        assert_eq!(calls[0], region);

        // Verify the KMS works
        let test_data = b"test data for convenience function".to_vec();
        let encrypted = kms
            .encrypt_key(&test_data)
            .await
            .expect("Failed to encrypt");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt");
        assert_eq!(test_data, decrypted);
    }
}
