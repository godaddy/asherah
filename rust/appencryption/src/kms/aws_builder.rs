use crate::Aead;
use crate::error::{Error, Result};
use crate::kms::aws::{AwsKms, AwsKmsClient, RegionalClient};
use std::sync::Arc;
use std::collections::HashMap;

/// Builder for the AWS KMS implementation
pub struct AwsKmsBuilder {
    /// Map of region -> ARN
    arn_map: HashMap<String, String>,

    /// AEAD implementation for data encryption
    crypto: Arc<dyn Aead>,

    /// Preferred region for KMS operations
    preferred_region: Option<String>,

    /// KMS clients for each region
    clients: HashMap<String, Arc<dyn AwsKmsClient>>,
}

impl AwsKmsBuilder {
    /// Creates a new AwsKmsBuilder with the given crypto implementation and ARN map
    pub fn new(crypto: Arc<dyn Aead>, arn_map: HashMap<String, String>) -> Self {
        if arn_map.is_empty() {
            panic!("arnMap must contain at least one entry");
        }

        Self {
            arn_map,
            crypto,
            preferred_region: None,
            clients: HashMap::new(),
        }
    }

    /// Sets the preferred region for KMS operations
    pub fn with_preferred_region(mut self, region: impl Into<String>) -> Self {
        self.preferred_region = Some(region.into());
        self
    }

    /// Adds a KMS client for a region
    pub fn with_kms_client(mut self, region: impl Into<String>, client: Arc<dyn AwsKmsClient>) -> Self {
        self.clients.insert(region.into(), client);
        self
    }

    /// Builds the AWS KMS implementation
    pub fn build(self) -> Result<AwsKms> {
        // Check that we have a preferred region if we have multiple regions
        if self.arn_map.len() > 1 && self.preferred_region.is_none() {
            return Err(Error::Kms("Preferred region must be set when using multiple regions".into()));
        }

        // Get the preferred region
        let preferred_region = self.preferred_region.unwrap_or_else(|| {
            self.arn_map.keys().next().unwrap().clone()
        });

        // Create the regional clients
        let mut regional_clients = Vec::new();

        // First, add the preferred region
        if let Some(arn) = self.arn_map.get(&preferred_region) {
            if let Some(client) = self.clients.get(&preferred_region) {
                regional_clients.push(RegionalClient::new(
                    client.clone(),
                    arn.clone(),
                ));
            } else {
                return Err(Error::Kms(format!("No KMS client provided for preferred region: {}", preferred_region)));
            }
        } else {
            return Err(Error::Kms(format!("Preferred region not found in ARN map: {}", preferred_region)));
        }

        // Then add the rest of the regions
        for (region, arn) in &self.arn_map {
            if region == &preferred_region {
                continue;
            }

            if let Some(client) = self.clients.get(region) {
                regional_clients.push(RegionalClient::new(
                    client.clone(),
                    arn.clone(),
                ));
            } else {
                return Err(Error::Kms(format!("No KMS client provided for region: {}", region)));
            }
        }

        Ok(AwsKms::new(regional_clients, self.crypto))
    }
}