#[cfg(test)]
mod tests {
    use crate::crypto::Aes256GcmAead as Aes256Gcm;
    use crate::error::Result;
    use crate::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
    use crate::plugins::aws_v2::kms::client::GenerateDataKeyResponse;
    use crate::KeyManagementService;
    use async_trait::async_trait;
    use aws_sdk_kms::config::Region;
    use aws_config::SdkConfig;
    use aws_sdk_kms::Client as AwsSdkKmsClient;
    use std::cell::RefCell;
    use std::collections::HashMap;
    use std::rc::Rc;
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
            // For simplicity, we're not actually tracking the endpoint
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

    // Custom factory function for testing
    struct TestFactory {
        created_clients: Mutex<Vec<String>>,
        mock_clients: HashMap<String, Arc<MockKmsClient>>,
    }

    impl TestFactory {
        fn new(mock_clients: HashMap<String, Arc<MockKmsClient>>) -> Self {
            Self {
                created_clients: Mutex::new(Vec::new()),
                mock_clients,
            }
        }

        fn factory_fn(&self) -> impl Fn(SdkConfig) -> Arc<dyn AwsKmsClient> + '_ {
            |config| {
                let region = config
                    .region()
                    .map(|r| r.to_string())
                    .unwrap_or_else(|| "unknown".to_string());
                self.created_clients.lock().unwrap().push(region.clone());

                if let Some(client) = self.mock_clients.get(&region) {
                    client.clone()
                } else {
                    panic!("No mock client configured for region: {}", region);
                }
            }
        }

        fn clients_created(&self) -> Vec<String> {
            self.created_clients.lock().unwrap().clone()
        }
    }

    #[tokio::test]
    async fn test_builder_with_single_region() {
        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let mock_client = Arc::new(MockKmsClient::new("us-west-2", master_key));

        // Create a test factory
        let mut mock_clients = HashMap::new();
        mock_clients.insert("us-west-2".to_string(), mock_client);
        let test_factory = TestFactory::new(mock_clients);

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with minimal configuration
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_kms_factory(test_factory.factory_fn())
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Test the preferred region (should be the only region)
        assert_eq!(kms.preferred_region(), "us-west-2");

        // Test encrypt/decrypt round trip
        let data = b"test data for encryption".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);

        // Check that only one client was created
        assert_eq!(test_factory.clients_created().len(), 1);
        assert_eq!(test_factory.clients_created()[0], "us-west-2");
    }

    #[tokio::test]
    async fn test_builder_with_multiple_regions() {
        // Create mock clients
        let master_key1 = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let master_key2 = vec![16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];

        let mock_client1 = Arc::new(MockKmsClient::new("us-west-2", master_key1));
        let mock_client2 = Arc::new(MockKmsClient::new("us-east-1", master_key2));

        // Create a test factory
        let mut mock_clients = HashMap::new();
        mock_clients.insert("us-west-2".to_string(), mock_client1);
        mock_clients.insert("us-east-1".to_string(), mock_client2);
        let test_factory = TestFactory::new(mock_clients);

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
            .with_kms_factory(test_factory.factory_fn())
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Test the preferred region
        assert_eq!(kms.preferred_region(), "us-west-2");

        // Test encrypt/decrypt round trip
        let data = b"test data for multi-region encryption".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);

        // Check that two clients were created
        assert_eq!(test_factory.clients_created().len(), 2);
        // Preferred region should be first
        assert_eq!(test_factory.clients_created()[0], "us-west-2");
        assert_eq!(test_factory.clients_created()[1], "us-east-1");
    }

    #[tokio::test]
    async fn test_builder_requires_preferred_region_with_multiple_regions() {
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

        // Create AWS KMS without preferred region
        let result = AwsKmsBuilder::new(crypto, arn_map).build().await;

        // Build should fail because preferred region is required with multiple regions
        assert!(result.is_err());

        match result {
            Err(crate::error::Error::Kms(msg)) => {
                assert!(msg.contains("Preferred region must be set when using multiple regions"));
            }
            _ => panic!("Expected KMS error with message about preferred region"),
        }
    }

    #[tokio::test]
    async fn test_builder_with_custom_config() {
        // Create custom SDK config
        let sdk_config = aws_config::from_env()
            .region(Region::new("us-west-2"))
            .load()
            .await;

        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let mock_client = Arc::new(MockKmsClient::new("us-west-2", master_key));

        // Create a test factory
        let mut mock_clients = HashMap::new();
        mock_clients.insert("us-west-2".to_string(), mock_client);
        let test_factory = TestFactory::new(mock_clients);

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with custom config
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_aws_config(sdk_config)
            .with_kms_factory(test_factory.factory_fn())
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Test encrypt/decrypt round trip
        let data = b"test data with custom config".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);
    }

    #[tokio::test]
    async fn test_builder_with_timeout() {
        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        // Create a factory that tracks config options
        let factory = |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            let client = MockKmsClient::new("us-west-2", master_key.clone()).with_config(&config);

            // Extract the timeout from config
            let timeout = config
                .timeout_config()
                .and_then(|tc| tc.operation_timeout())
                .cloned();

            // Assert that timeout was set
            assert_eq!(timeout, Some(Duration::from_secs(5)));

            Arc::new(client)
        };

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with timeout
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_timeout(Duration::from_secs(5))
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Just check that it builds, the factory verifies the timeout
        assert_eq!(kms.preferred_region(), "us-west-2");
    }

    #[tokio::test]
    async fn test_builder_with_retry_config() {
        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        // Create a factory that tracks config options
        let factory = |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            let client = MockKmsClient::new("us-west-2", master_key.clone()).with_config(&config);

            // Extract the retry config from config
            let max_retries = config.retry_config().map(|rc| rc.max_attempts());

            // Assert that retry config was set
            assert_eq!(max_retries, Some(3));

            Arc::new(client)
        };

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with retry config
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_retry_config(3, 100)
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Just check that it builds, the factory verifies the retry config
        assert_eq!(kms.preferred_region(), "us-west-2");
    }

    #[tokio::test]
    async fn test_builder_with_custom_endpoint() {
        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let mock_client = MockKmsClient::new("us-west-2", master_key.clone())
            .with_endpoint("http://localhost:4566");

        let mock_client_arc = Arc::new(mock_client);

        // Create a tracked factory function
        let endpoint_checked = Rc::new(RefCell::new(false));
        let endpoint_checked_clone = endpoint_checked.clone();

        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            // Endpoint validation happens in the builder.create_regional_client method
            // so we just flag that this factory was called
            *endpoint_checked_clone.borrow_mut() = true;
            mock_client_arc.clone()
        };

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Create AWS KMS with custom endpoint
        let kms = AwsKmsBuilder::new(crypto, arn_map)
            .with_endpoint("us-west-2", "http://localhost:4566")
            .with_kms_factory(factory)
            .build()
            .await
            .expect("Failed to build AWS KMS");

        // Verify the factory was called
        assert!(*endpoint_checked.borrow());

        // Test encrypt/decrypt round trip
        let data = b"test data with custom endpoint".to_vec();
        let encrypted = kms.encrypt_key(&data).await.expect("Failed to encrypt key");
        let decrypted = kms
            .decrypt_key(&encrypted)
            .await
            .expect("Failed to decrypt key");

        assert_eq!(data, decrypted);
    }

    #[tokio::test]
    async fn test_new_aws_kms_convenience_function() {
        use crate::plugins::aws_v2::kms::new_aws_kms;

        // Create mock client
        let master_key = vec![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        let mock_client = Arc::new(MockKmsClient::new("us-west-2", master_key));

        // Create a factory function
        let factory_called = Rc::new(RefCell::new(false));
        let factory_called_clone = factory_called.clone();

        // Patch the default_kms_factory for this test
        // In a real application we would use with_kms_factory
        tokio::task::spawn(async move {
            // TODO: This is just a placeholder for demonstration
            // In a real test we would use a proper mocking framework
            *factory_called_clone.borrow_mut() = true;
        });

        // Create ARN map
        let mut arn_map = HashMap::new();
        arn_map.insert(
            "us-west-2".to_string(),
            "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string(),
        );

        // Create AEAD crypto
        let crypto = Arc::new(Aes256Gcm::new());

        // Use the convenience function
        new_aws_kms(crypto, "us-west-2", arn_map)
            .await
            .expect_err("Should fail without proper mocking");

        // In a real test, we would verify the KMS works
    }
}
