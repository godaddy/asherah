use crate::error::{Error, Result};
use crate::plugins::aws_v2::kms::client::{
    AwsKms, AwsKmsClient, RegionalClient, StandardAwsKmsClient,
};
use crate::Aead;
use aws_config::timeout::TimeoutConfig;
use aws_config::{self, SdkConfig};
use aws_sdk_kms::config::Region;
use aws_sdk_kms::Client as AwsSdkKmsClient;
use std::collections::HashMap;
use std::sync::Arc;
use std::time::Duration;

/// Function type that creates a new AWS KMS client from an SDK configuration
pub type KmsFactory = dyn Fn(SdkConfig) -> Arc<dyn AwsKmsClient> + Send + Sync;

/// Default KMS factory that creates a StandardAwsKmsClient using the provided SDK config
fn default_kms_factory(config: SdkConfig) -> Arc<dyn AwsKmsClient> {
    let region = config
        .region()
        .map(|r| r.as_ref().to_string())
        .unwrap_or_else(|| "us-east-1".to_string());
    let client = AwsSdkKmsClient::new(&config);
    Arc::new(StandardAwsKmsClient::new(client, region))
}

/// Builder for the AWS KMS implementation
///
/// This builder provides a fluent API for configuring an AWS KMS implementation
/// with various options like preferred region, AWS credentials, retries, and more.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use std::sync::Arc;
/// use appencryption::crypto::Aes256GcmAead;
/// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
/// use std::time::Duration;
///
/// #[tokio::main]
/// async fn main() -> Result<(), Box<dyn std::error::Error>> {
///     // Create an ARN map with KMS keys for each region
///     let mut arn_map = HashMap::new();
///     arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
///     arn_map.insert("us-east-1".to_string(), "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string());
///
///     // Create AEAD crypto
///     let crypto = Arc::new(Aes256GcmAead::new());
///
///     // Build AWS KMS with configuration options
///     let kms = AwsKmsBuilder::new(crypto, arn_map)
///         .with_preferred_region("us-west-2")
///         .with_retry_config(3, 100) // 3 retries with 100ms base delay
///         .with_timeout(Duration::from_secs(5)) // 5 second timeout
///         .build()
///         .await?;
///
///     Ok(())
/// }
/// ```
pub struct AwsKmsBuilder {
    /// Map of region -> ARN
    arn_map: HashMap<String, String>,

    /// AEAD implementation for data encryption
    crypto: Arc<dyn Aead>,

    /// Preferred region for KMS operations
    preferred_region: Option<String>,

    /// Custom KMS factory for testing or customization
    factory: Option<Arc<KmsFactory>>,

    /// Custom AWS SDK configuration
    sdk_config: Option<SdkConfig>,

    /// Flag indicating if we're using a custom SDK config
    using_custom_config: bool,

    /// Custom timeout configuration
    timeout_config: Option<TimeoutConfig>,

    /// Maximum number of retries for AWS operations
    max_retries: Option<u32>,

    /// Base retry delay in milliseconds
    retry_base_delay: Option<u64>,

    /// Custom endpoint URL for specific regions
    endpoints: HashMap<String, String>,
}

impl AwsKmsBuilder {
    /// Creates a new AwsKmsBuilder with the given crypto implementation and ARN map
    ///
    /// The ARN map must contain at least one entry mapping a region to a KMS key ARN.
    ///
    /// # Panics
    ///
    /// Panics if the ARN map is empty.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// // Create an ARN map with a KMS key ARN for a region
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// // Create AEAD crypto
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// // Create a new builder
    /// let builder = AwsKmsBuilder::new(crypto, arn_map);
    /// ```
    pub fn new(crypto: Arc<dyn Aead>, arn_map: HashMap<String, String>) -> Self {
        if arn_map.is_empty() {
            log::error!("arnMap must contain at least one entry");
            panic!("ARN map is empty, which is not allowed");
        }

        Self {
            arn_map,
            crypto,
            preferred_region: None,
            factory: None,
            sdk_config: None,
            using_custom_config: false,
            timeout_config: None,
            max_retries: None,
            retry_base_delay: None,
            endpoints: HashMap::new(),
        }
    }

    /// Sets the preferred region for KMS operations
    ///
    /// This is required when using multiple regions. The preferred region will be
    /// tried first for both encryption and decryption operations.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// // Create an ARN map with KMS keys for multiple regions
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    /// arn_map.insert("us-east-1".to_string(), "arn:aws:kms:us-east-1:123456789012:key/efgh-5678".to_string());
    ///
    /// // Create AEAD crypto
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// // Set preferred region for failover
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_preferred_region("us-west-2");
    /// ```
    pub fn with_preferred_region(mut self, region: impl Into<String>) -> Self {
        self.preferred_region = Some(region.into());
        self
    }

    /// Sets a custom KMS factory function for creating AWS KMS clients
    ///
    /// This is primarily used for testing, but can also be used to customize
    /// how KMS clients are created.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
    /// use aws_config::SdkConfig;
    ///
    /// // Define a custom KMS factory function
    /// let factory = |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
    ///     // Create a custom KMS client implementation
    ///     // This example would be completed with a real implementation
    ///     todo!("Implement custom KMS client")
    /// };
    ///
    /// // Create builder with custom factory
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_kms_factory(factory);
    /// ```
    pub fn with_kms_factory<F>(mut self, factory: F) -> Self
    where
        F: Fn(SdkConfig) -> Arc<dyn AwsKmsClient> + Send + Sync + 'static,
    {
        self.factory = Some(Arc::new(factory));
        self
    }

    /// Sets a custom AWS SDK configuration
    ///
    /// This allows full customization of the AWS SDK configuration used to create
    /// KMS clients. If not set, the default AWS SDK configuration will be used.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    /// use aws_config::SdkConfig;
    ///
    /// #[tokio::main]
    /// async fn main() {
    ///     // Load a custom SDK configuration
    ///     let sdk_config = aws_config::from_env().load().await;
    ///
    ///     // Create builder with custom SDK config
    ///     let mut arn_map = HashMap::new();
    ///     arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    ///     let crypto = Arc::new(Aes256GcmAead::new());
    ///
    ///     let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///         .with_aws_config(sdk_config);
    /// }
    /// ```
    pub fn with_aws_config(mut self, config: SdkConfig) -> Self {
        self.sdk_config = Some(config);
        self.using_custom_config = true;
        self
    }

    /// Sets a custom timeout for AWS SDK operations
    ///
    /// This configures the timeout for all API requests made by the AWS SDK.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use std::time::Duration;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// // Create builder with a 5 second timeout
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_timeout(Duration::from_secs(5));
    /// ```
    pub fn with_timeout(mut self, timeout: Duration) -> Self {
        self.timeout_config = Some(
            TimeoutConfig::builder()
                .operation_timeout(timeout)
                .operation_attempt_timeout(timeout)
                .build(),
        );
        self
    }

    /// Configures retry behavior for AWS operations
    ///
    /// # Arguments
    ///
    /// * `max_retries` - Maximum number of retry attempts
    /// * `base_delay_ms` - Base delay between retries in milliseconds
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// // Create builder with retry configuration: 3 retries with 100ms base delay
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_retry_config(3, 100);
    /// ```
    pub fn with_retry_config(mut self, max_retries: u32, base_delay_ms: u64) -> Self {
        self.max_retries = Some(max_retries);
        self.retry_base_delay = Some(base_delay_ms);
        self
    }

    /// Sets a custom endpoint URL for a specific region
    ///
    /// This is useful for testing with local AWS mock services or
    /// for connecting to custom AWS endpoints.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// // Create builder with a custom endpoint for localhost testing
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_endpoint("us-west-2", "http://localhost:4566");
    /// ```
    pub fn with_endpoint(mut self, region: impl Into<String>, endpoint: impl Into<String>) -> Self {
        self.endpoints.insert(region.into(), endpoint.into());
        self
    }

    /// Sets a specific KMS client for a region
    ///
    /// This is primarily used for testing to inject mock KMS clients.
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::{AwsKmsBuilder, AwsKmsClient};
    ///
    /// // Create a mock KMS client
    /// let mock_client = // ... create a mock implementation of AwsKmsClient
    ///     todo!("Create mock KMS client");
    ///
    /// // Create builder with the mock client
    /// let mut arn_map = HashMap::new();
    /// arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    /// let crypto = Arc::new(Aes256GcmAead::new());
    ///
    /// let builder = AwsKmsBuilder::new(crypto, arn_map)
    ///     .with_kms_client("us-west-2", Arc::new(mock_client));
    /// ```
    pub fn with_kms_client(
        mut self,
        region: impl Into<String>,
        client: Arc<dyn AwsKmsClient>,
    ) -> Self {
        let region_str = region.into();

        // Create a factory that will return the provided client for the specified region
        let factory = move |config: SdkConfig| -> Arc<dyn AwsKmsClient> {
            let config_region = config
                .region()
                .map(|r| r.as_ref().to_string())
                .unwrap_or_else(|| "unknown".to_string());

            if config_region == region_str {
                client.clone()
            } else {
                // For other regions, use the default factory
                default_kms_factory(config)
            }
        };

        self.factory = Some(Arc::new(factory));
        self
    }

    /// Builds the AWS KMS implementation
    ///
    /// This asynchronously builds the AWS KMS implementation, creating the necessary
    /// KMS clients for each region specified in the ARN map.
    ///
    /// # Errors
    ///
    /// Returns an error if:
    /// - Multiple regions are used without setting a preferred region
    /// - AWS SDK configuration fails to load
    ///
    /// # Examples
    ///
    /// ```
    /// use std::collections::HashMap;
    /// use std::sync::Arc;
    /// use appencryption::crypto::Aes256GcmAead;
    /// use appencryption::plugins::aws_v2::kms::AwsKmsBuilder;
    ///
    /// #[tokio::main]
    /// async fn main() -> Result<(), Box<dyn std::error::Error>> {
    ///     let mut arn_map = HashMap::new();
    ///     arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
    ///
    ///     let crypto = Arc::new(Aes256GcmAead::new());
    ///
    ///     let kms = AwsKmsBuilder::new(crypto, arn_map)
    ///         .build()
    ///         .await?;
    ///
    ///     Ok(())
    /// }
    /// ```
    pub async fn build(mut self) -> Result<AwsKms> {
        // Check that we have a preferred region if we have multiple regions
        if self.arn_map.len() > 1 && self.preferred_region.is_none() {
            return Err(Error::Kms(
                "Preferred region must be set when using multiple regions".into(),
            ));
        }

        // Get the factory or use the default
        let factory = self
            .factory
            .clone()
            .unwrap_or_else(|| Arc::new(default_kms_factory));

        // Load SDK configuration
        let sdk_config = if self.using_custom_config {
            self.sdk_config
                .take()
                .expect("SDK config must be set when using_custom_config is true")
        } else {
            let mut config_loader = aws_config::from_env();

            // Add timeout configuration if specified
            if let Some(timeout_config) = self.timeout_config.clone() {
                config_loader = config_loader.timeout_config(timeout_config);
            }

            // Add retry configuration if specified
            if let (Some(max_retries), Some(base_delay)) = (self.max_retries, self.retry_base_delay)
            {
                let retry_config = aws_config::retry::RetryConfig::standard()
                    .with_max_attempts(max_retries)
                    .with_initial_backoff(Duration::from_millis(base_delay));

                config_loader = config_loader.retry_config(retry_config);
            }

            config_loader.load().await
        };

        // Get the preferred region
        let preferred_region = self.preferred_region.clone().unwrap_or_else(|| {
            self.arn_map
                .keys()
                .next()
                .expect("ARN map must contain at least one key")
                .clone()
        });

        // Create the regional clients
        let mut regional_clients = Vec::new();

        // First create the preferred region client
        if let Some(arn) = self.arn_map.get(&preferred_region) {
            let regional_client = self
                .create_regional_client(&preferred_region, arn, &sdk_config, &factory)
                .await?;
            regional_clients.push(regional_client);
        } else {
            return Err(Error::Kms(format!(
                "Preferred region not found in ARN map: {}",
                preferred_region
            )));
        }

        // Then create clients for the rest of the regions
        for (region, arn) in &self.arn_map {
            if region == &preferred_region {
                continue;
            }

            let regional_client = self
                .create_regional_client(region, arn, &sdk_config, &factory)
                .await?;
            regional_clients.push(regional_client);
        }

        Ok(AwsKms::new(regional_clients, self.crypto))
    }

    // Helper method to create a regional client
    async fn create_regional_client(
        &self,
        region: &str,
        arn: &str,
        base_config: &SdkConfig,
        factory: &Arc<KmsFactory>,
    ) -> Result<RegionalClient> {
        // Create a copy of the config with the region
        let mut config_builder = aws_config::from_env().region(Region::new(region.to_string()));

        // Add credentials provider if available
        if let Some(credentials_provider) = base_config.credentials_provider() {
            config_builder = config_builder.credentials_provider(credentials_provider.clone());
        }

        // Add retry configuration if available
        if let Some(retry_config) = base_config.retry_config() {
            config_builder = config_builder.retry_config(retry_config.clone());
        }

        // Add timeout configuration if available
        if let Some(timeout_config) = base_config.timeout_config() {
            config_builder = config_builder.timeout_config(timeout_config.clone());
        }

        // Add endpoint override if we have a custom endpoint for this region
        if let Some(endpoint) = self.endpoints.get(region) {
            // Use endpoint URL override
            config_builder = config_builder.endpoint_url(endpoint);
        }

        // Build the regional config
        let config = config_builder.load().await;

        // Create the client using the factory
        let client = factory(config);

        Ok(RegionalClient::new(client, arn.to_string()))
    }
}

/// Convenience function to create an AWS KMS implementation with default settings
///
/// This is equivalent to using the AwsKmsBuilder with a preferred region.
///
/// # Arguments
///
/// * `crypto` - AEAD implementation for data encryption
/// * `preferred_region` - Preferred region for KMS operations
/// * `arn_map` - Map of region -> ARN
///
/// # Errors
///
/// Returns an error if AWS SDK configuration fails to load.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use std::sync::Arc;
/// use appencryption::crypto::Aes256GcmAead;
/// use appencryption::plugins::aws_v2::kms::new_aws_kms;
///
/// #[tokio::main]
/// async fn main() -> Result<(), Box<dyn std::error::Error>> {
///     let mut arn_map = HashMap::new();
///     arn_map.insert("us-west-2".to_string(), "arn:aws:kms:us-west-2:123456789012:key/abcd-1234".to_string());
///
///     let crypto = Arc::new(Aes256GcmAead::new());
///
///     let kms = new_aws_kms(crypto, "us-west-2", arn_map).await?;
///
///     Ok(())
/// }
/// ```
pub async fn new_aws_kms(
    crypto: Arc<dyn Aead>,
    preferred_region: impl Into<String>,
    arn_map: HashMap<String, String>,
) -> Result<AwsKms> {
    AwsKmsBuilder::new(crypto, arn_map)
        .with_preferred_region(preferred_region)
        .build()
        .await
}
