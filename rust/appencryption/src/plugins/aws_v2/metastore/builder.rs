use crate::error::{Error, Result};
use crate::plugins::aws_v2::metastore::client::{
    DynamoDbClient, DynamoDbItem, DynamoDbKey, KEY_RECORD, PARTITION_KEY, SORT_KEY,
};
use async_trait::async_trait;
use aws_sdk_dynamodb::config::Region;
use aws_sdk_dynamodb::{
    types::{AttributeValue, ReturnValue},
    Client as AwsDynamoDbClient,
};
use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, Ordering};

/// Standard DynamoDB client implementation using AWS SDK v2
pub struct StandardDynamoDbClient {
    /// AWS SDK DynamoDB client
    client: AwsDynamoDbClient,

    /// AWS region for this client
    region: String,

    /// Health flag for this client
    healthy: AtomicBool,
}

impl StandardDynamoDbClient {
    /// Creates a new StandardDynamoDbClient
    pub fn new(client: AwsDynamoDbClient, region: String) -> Self {
        Self {
            client,
            region,
            healthy: AtomicBool::new(true),
        }
    }

    /// Converts a string to a DynamoDB AttributeValue
    fn to_string_attribute(value: String) -> AttributeValue {
        AttributeValue::S(value)
    }

    /// Converts a number to a DynamoDB AttributeValue
    fn to_number_attribute(value: i64) -> AttributeValue {
        AttributeValue::N(value.to_string())
    }

    /// Builds a key attributes map for DynamoDB operations
    fn build_key_attributes(key: &DynamoDbKey) -> HashMap<String, AttributeValue> {
        let mut attributes = HashMap::new();

        attributes.insert(
            PARTITION_KEY.to_string(),
            Self::to_string_attribute(key.id.clone()),
        );

        attributes.insert(SORT_KEY.to_string(), Self::to_number_attribute(key.created));

        attributes
    }
}

#[async_trait]
impl DynamoDbClient for StandardDynamoDbClient {
    async fn get_item(&self, table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>> {
        // Build key attributes
        let key_attributes = Self::build_key_attributes(&key);

        // Execute the get item request
        let result = self
            .client
            .get_item()
            .table_name(table_name)
            .set_key(Some(key_attributes))
            .send()
            .await
            .map_err(|e| Error::Metastore(format!("DynamoDB get_item error: {}", e)))?;

        // If no item was found, return None
        if result.item().is_none() {
            return Ok(None);
        }

        // Parse the result
        let item = result.item().unwrap();

        // Extract key record attribute
        let key_record_json = item
            .get(KEY_RECORD)
            .and_then(|av| av.as_s().ok())
            .ok_or_else(|| Error::Metastore("Missing or invalid KeyRecord attribute".into()))?;

        // Deserialize the key record from the JSON string
        let db_envelope = serde_json::from_str(key_record_json)
            .map_err(|e| Error::Metastore(format!("Error deserializing key record: {}", e)))?;

        // Create the item
        let db_item = DynamoDbItem {
            id: key.id,
            created: key.created,
            key_record: db_envelope,
        };

        Ok(Some(db_item))
    }

    async fn put_item_if_not_exists(&self, table_name: &str, item: DynamoDbItem) -> Result<bool> {
        // Build key attributes
        let mut key_attributes = HashMap::new();

        key_attributes.insert("Id".to_string(), Self::to_string_attribute(item.id.clone()));

        key_attributes.insert(
            "Created".to_string(),
            Self::to_number_attribute(item.created),
        );

        // Serialize the key record to JSON
        let key_record_json = serde_json::to_string(&item.key_record)
            .map_err(|e| Error::Metastore(format!("Error serializing key record: {}", e)))?;

        // Use JSON string as the attribute value
        key_attributes.insert(
            KEY_RECORD.to_string(),
            Self::to_string_attribute(key_record_json),
        );

        // Build condition expression
        let condition_expression = "attribute_not_exists(Id) AND attribute_not_exists(Created)";

        // Execute the put item request
        match self
            .client
            .put_item()
            .table_name(table_name)
            .set_item(Some(key_attributes))
            .condition_expression(condition_expression)
            .return_values(ReturnValue::None)
            .send()
            .await
        {
            Ok(_) => Ok(true),
            Err(err) => {
                // Check if it's a condition check failure (item already exists)
                if err.to_string().contains("ConditionalCheckFailedException") {
                    Ok(false)
                } else {
                    Err(Error::Metastore(format!(
                        "DynamoDB put_item error: {}",
                        err
                    )))
                }
            }
        }
    }

    async fn query_latest(
        &self,
        table_name: &str,
        partition_key: &str,
    ) -> Result<Vec<DynamoDbItem>> {
        // Build key condition expression
        let key_condition_expression = "Id = :id";

        // Build expression attribute values
        let mut expression_attr_values = HashMap::new();
        expression_attr_values.insert(
            ":id".to_string(),
            Self::to_string_attribute(partition_key.to_string()),
        );

        // Execute the query
        let result = self
            .client
            .query()
            .table_name(table_name)
            .key_condition_expression(key_condition_expression)
            .set_expression_attribute_values(Some(expression_attr_values))
            .limit(1) // We only need the latest
            .scan_index_forward(false) // Descending order (newest first)
            .send()
            .await
            .map_err(|e| Error::Metastore(format!("DynamoDB query error: {}", e)))?;

        // Parse the results
        let mut items = Vec::new();

        if let Some(result_items) = result.items() {
            for item in result_items {
                // Extract ID
                let id = item
                    .get("Id")
                    .and_then(|av| av.as_s().ok())
                    .ok_or_else(|| Error::Metastore("Missing Id attribute".into()))?
                    .clone();

                // Extract Created
                let created = item
                    .get("Created")
                    .and_then(|av| av.as_n().ok())
                    .ok_or_else(|| Error::Metastore("Missing Created attribute".into()))?
                    .parse::<i64>()
                    .map_err(|e| Error::Metastore(format!("Invalid Created value: {}", e)))?;

                // Extract key record
                let key_record_json = item
                    .get(KEY_RECORD)
                    .and_then(|av| av.as_s().ok())
                    .ok_or_else(|| {
                        Error::Metastore("Missing or invalid KeyRecord attribute".into())
                    })?;

                // Deserialize key record
                let db_envelope = serde_json::from_str(key_record_json).map_err(|e| {
                    Error::Metastore(format!("Error deserializing key record: {}", e))
                })?;

                // Create item
                let db_item = DynamoDbItem {
                    id,
                    created,
                    key_record: db_envelope,
                };

                items.push(db_item);
            }
        }

        Ok(items)
    }

    fn region(&self) -> &str {
        &self.region
    }

    fn is_healthy(&self) -> bool {
        self.healthy.load(Ordering::Relaxed)
    }

    async fn health_check(&self) -> Result<bool> {
        // Perform a simple describe table operation to check health
        match self.client.describe_endpoints().send().await {
            Ok(_) => {
                self.healthy.store(true, Ordering::Relaxed);
                Ok(true)
            }
            Err(e) => {
                log::debug!("DynamoDB health check failed: {}", e);
                self.healthy.store(false, Ordering::Relaxed);
                Ok(false)
            }
        }
    }
}

/// Builder for DynamoDB client
pub struct DynamoDbClientBuilder {
    /// AWS config for the client
    config: Option<aws_config::SdkConfig>,

    /// Region for the client
    region: String,
}

impl DynamoDbClientBuilder {
    /// Creates a new builder with the given region
    pub fn new(region: impl Into<String>) -> Self {
        Self {
            config: None,
            region: region.into(),
        }
    }

    /// Sets the SDK config for the client
    pub fn with_config(mut self, config: aws_config::SdkConfig) -> Self {
        self.config = Some(config);
        self
    }

    /// Builds the DynamoDB client
    pub async fn build(self) -> Result<StandardDynamoDbClient> {
        let config = if let Some(config) = self.config {
            config
        } else {
            // Load config from environment
            aws_config::from_env()
                .region(Region::new(self.region.clone()))
                .load()
                .await
        };

        let client = AwsDynamoDbClient::new(&config);

        Ok(StandardDynamoDbClient::new(client, self.region))
    }
}
