use crate::error::{Error, Result};
use crate::metastore::dynamodb::{DynamoDbClient, DynamoDbEnvelope, DynamoDbItem, DynamoDbKey, DynamoDbKeyMeta};
use async_trait::async_trait;
use aws_config::meta::region::RegionProviderChain;
use aws_sdk_dynamodb::types::AttributeValue;
use aws_sdk_dynamodb::{Client, config::Region};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use serde::{Serialize, Deserialize};
use base64::{engine::general_purpose, Engine as _};

/// Default timeout for DynamoDB operations
const DEFAULT_TIMEOUT: Duration = Duration::from_secs(2);

/// Standard DynamoDB client implementation
pub struct StandardDynamoDbClient {
    /// AWS DynamoDB client
    client: Client,
    
    /// AWS region for this client
    region: String,
    
    /// Client health status
    healthy: Mutex<bool>,
    
    /// Request timeout
    timeout: Duration,
}

impl StandardDynamoDbClient {
    /// Create a new DynamoDB client with the specified region
    pub async fn new(region: &str) -> Result<Self> {
        let region_provider = RegionProviderChain::first_try(Region::new(region.to_string()));
        
        let config = aws_config::from_env().region(region_provider).load().await;
        let client = Client::new(&config);
        
        Ok(Self {
            client,
            region: region.to_string(),
            healthy: Mutex::new(true), // Assume healthy by default
            timeout: DEFAULT_TIMEOUT,
        })
    }
    
    /// Create a new DynamoDB client with the specified region and endpoint
    pub async fn with_endpoint(region: &str, endpoint: &str) -> Result<Self> {
        let region_provider = RegionProviderChain::first_try(Region::new(region.to_string()));
        
        let config = aws_config::from_env()
            .region(region_provider)
            .endpoint_url(endpoint)
            .load()
            .await;
            
        let client = Client::new(&config);
        
        Ok(Self {
            client,
            region: region.to_string(),
            healthy: Mutex::new(true), // Assume healthy by default
            timeout: DEFAULT_TIMEOUT,
        })
    }
    
    /// Set the timeout for DynamoDB operations
    pub fn with_timeout(mut self, timeout: Duration) -> Self {
        self.timeout = timeout;
        self
    }
    
    /// Get the AWS DynamoDB client
    pub fn get_client(&self) -> &Client {
        &self.client
    }
}

// Helper for converting AWS DynamoDB attribute values to our types
fn from_dynamodb_item(item: HashMap<String, AttributeValue>) -> Result<DynamoDbItem> {
    // Extract the required fields
    let id = match item.get("Id") {
        Some(AttributeValue::S(s)) => s.clone(),
        _ => return Err(Error::Metastore("Missing or invalid Id attribute".into())),
    };
    
    let created = match item.get("Created") {
        Some(AttributeValue::N(n)) => n.parse::<i64>()
            .map_err(|e| Error::Metastore(format!("Invalid Created attribute: {}", e)))?,
        _ => return Err(Error::Metastore("Missing or invalid Created attribute".into())),
    };
    
    // Extract the key record map
    let key_record_map = match item.get("KeyRecord") {
        Some(AttributeValue::M(m)) => m,
        _ => return Err(Error::Metastore("Missing or invalid KeyRecord attribute".into())),
    };
    
    // Extract the key record fields
    let revoked = match key_record_map.get("Revoked") {
        Some(AttributeValue::Bool(b)) => Some(*b),
        None => None,
        _ => return Err(Error::Metastore("Invalid Revoked attribute".into())),
    };
    
    let kr_created = match key_record_map.get("Created") {
        Some(AttributeValue::N(n)) => n.parse::<i64>()
            .map_err(|e| Error::Metastore(format!("Invalid KeyRecord.Created attribute: {}", e)))?,
        _ => return Err(Error::Metastore("Missing or invalid KeyRecord.Created attribute".into())),
    };
    
    let encrypted_key = match key_record_map.get("Key") {
        Some(AttributeValue::S(s)) => s.clone(),
        _ => return Err(Error::Metastore("Missing or invalid Key attribute".into())),
    };
    
    // Extract the parent key metadata if present
    let parent_key_meta = match key_record_map.get("ParentKeyMeta") {
        Some(AttributeValue::M(m)) => {
            let id = match m.get("KeyId") {
                Some(AttributeValue::S(s)) => s.clone(),
                _ => return Err(Error::Metastore("Invalid ParentKeyMeta.KeyId attribute".into())),
            };
            
            let created = match m.get("Created") {
                Some(AttributeValue::N(n)) => n.parse::<i64>()
                    .map_err(|e| Error::Metastore(format!("Invalid ParentKeyMeta.Created attribute: {}", e)))?,
                _ => return Err(Error::Metastore("Invalid ParentKeyMeta.Created attribute".into())),
            };
            
            Some(DynamoDbKeyMeta { id, created })
        },
        None => None,
        _ => return Err(Error::Metastore("Invalid ParentKeyMeta attribute".into())),
    };
    
    // Create the key record envelope
    let key_record = DynamoDbEnvelope {
        revoked,
        created: kr_created,
        encrypted_key,
        parent_key_meta,
    };
    
    // Create the DynamoDB item
    Ok(DynamoDbItem {
        id,
        created,
        key_record,
    })
}

// Helper for converting our types to AWS DynamoDB attribute values
fn to_dynamodb_item(item: &DynamoDbItem) -> HashMap<String, AttributeValue> {
    let mut result = HashMap::new();
    
    // Add the partition and sort keys
    result.insert("Id".to_string(), AttributeValue::S(item.id.clone()));
    result.insert("Created".to_string(), AttributeValue::N(item.created.to_string()));
    
    // Build the key record map
    let mut key_record = HashMap::new();
    
    if let Some(revoked) = item.key_record.revoked {
        key_record.insert("Revoked".to_string(), AttributeValue::Bool(revoked));
    }
    
    key_record.insert("Created".to_string(), AttributeValue::N(item.key_record.created.to_string()));
    key_record.insert("Key".to_string(), AttributeValue::S(item.key_record.encrypted_key.clone()));
    
    // Add parent key metadata if present
    if let Some(ref parent_key_meta) = item.key_record.parent_key_meta {
        let mut parent_meta = HashMap::new();
        parent_meta.insert("KeyId".to_string(), AttributeValue::S(parent_key_meta.id.clone()));
        parent_meta.insert("Created".to_string(), AttributeValue::N(parent_key_meta.created.to_string()));
        
        key_record.insert("ParentKeyMeta".to_string(), AttributeValue::M(parent_meta));
    }
    
    // Add the key record to the result
    result.insert("KeyRecord".to_string(), AttributeValue::M(key_record));
    
    result
}

#[async_trait]
impl DynamoDbClient for StandardDynamoDbClient {
    async fn get_item(&self, table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>> {
        let mut key_map = HashMap::new();
        key_map.insert("Id".to_string(), AttributeValue::S(key.id));
        key_map.insert("Created".to_string(), AttributeValue::N(key.created.to_string()));
        
        // Create the request with a consistent read
        let request = self.client.get_item()
            .table_name(table_name)
            .set_key(Some(key_map))
            .consistent_read(true);
            
        // Execute the request with timeout
        let response = tokio::time::timeout(
            self.timeout, 
            request.send()
        ).await.map_err(|e| Error::Metastore(format!("DynamoDB request timed out: {}", e)))?
        .map_err(|e| Error::Metastore(format!("DynamoDB get_item failed: {}", e)))?;
        
        // If no item was found, return None
        if response.item.is_none() || response.item.as_ref().unwrap().is_empty() {
            return Ok(None);
        }
        
        // Convert the response to our item type
        let item = from_dynamodb_item(response.item.unwrap())?;
        
        Ok(Some(item))
    }
    
    async fn put_item_if_not_exists(&self, table_name: &str, item: DynamoDbItem) -> Result<bool> {
        // Convert our item to DynamoDB attribute values
        let item_values = to_dynamodb_item(&item);
        
        // Create the request with a condition expression
        let request = self.client.put_item()
            .table_name(table_name)
            .set_item(Some(item_values))
            .condition_expression("attribute_not_exists(Id)");
            
        // Execute the request with timeout
        match tokio::time::timeout(self.timeout, request.send()).await {
            Ok(Ok(_)) => {
                // Item was successfully inserted
                Ok(true)
            },
            Ok(Err(e)) => {
                // Check if the error is a conditional check failed
                if e.to_string().contains("ConditionalCheckFailedException") {
                    // Item already exists
                    Ok(false)
                } else {
                    // Other error
                    Err(Error::Metastore(format!("DynamoDB put_item failed: {}", e)))
                }
            },
            Err(e) => {
                // Timeout
                Err(Error::Metastore(format!("DynamoDB request timed out: {}", e)))
            }
        }
    }
    
    async fn query_latest(&self, table_name: &str, partition_key: &str) -> Result<Vec<DynamoDbItem>> {
        // Create expression for the key condition
        let key_cond_expr = format!("Id = :id");
        
        // Create expression attribute values
        let mut expr_attr_values = HashMap::new();
        expr_attr_values.insert(":id".to_string(), AttributeValue::S(partition_key.to_string()));
        
        // Create the request with the expression
        let request = self.client.query()
            .table_name(table_name)
            .key_condition_expression(key_cond_expr)
            .set_expression_attribute_values(Some(expr_attr_values))
            .limit(1)
            .scan_index_forward(false) // Sort in descending order
            .consistent_read(true);
            
        // Execute the request with timeout
        let response = tokio::time::timeout(
            self.timeout, 
            request.send()
        ).await.map_err(|e| Error::Metastore(format!("DynamoDB request timed out: {}", e)))?
        .map_err(|e| Error::Metastore(format!("DynamoDB query failed: {}", e)))?;
        
        // If no items were found, return an empty vector
        if response.items.is_none() || response.items.as_ref().unwrap().is_empty() {
            return Ok(Vec::new());
        }
        
        // Convert the response items to our item type
        let mut items = Vec::new();
        for item in response.items.unwrap() {
            items.push(from_dynamodb_item(item)?);
        }
        
        Ok(items)
    }
    
    fn region(&self) -> &str {
        &self.region
    }
    
    fn is_healthy(&self) -> bool {
        *self.healthy.lock().unwrap()
    }
    
    async fn health_check(&self) -> Result<bool> {
        // Perform a lightweight operation to check if the client is healthy
        let request = self.client.list_tables().limit(1);
        
        match tokio::time::timeout(self.timeout, request.send()).await {
            Ok(Ok(_)) => {
                // Update health status
                let mut healthy = self.healthy.lock().unwrap();
                *healthy = true;
                Ok(true)
            },
            Ok(Err(e)) => {
                // Update health status
                let mut healthy = self.healthy.lock().unwrap();
                *healthy = false;
                Err(Error::Metastore(format!("DynamoDB health check failed: {}", e)))
            },
            Err(e) => {
                // Update health status
                let mut healthy = self.healthy.lock().unwrap();
                *healthy = false;
                Err(Error::Metastore(format!("DynamoDB health check timed out: {}", e)))
            }
        }
    }
}

/// Builder for creating DynamoDB clients with multiple regions
pub struct DynamoDbClientBuilder {
    /// Primary region for the client
    primary_region: String,
    
    /// Replica regions for the client
    replica_regions: Vec<String>,
    
    /// Endpoint override for localstack/testing
    endpoint: Option<String>,
    
    /// Request timeout
    timeout: Duration,
}

impl DynamoDbClientBuilder {
    /// Create a new DynamoDB client builder with the specified primary region
    pub fn new(primary_region: &str) -> Self {
        Self {
            primary_region: primary_region.to_string(),
            replica_regions: Vec::new(),
            endpoint: None,
            timeout: DEFAULT_TIMEOUT,
        }
    }
    
    /// Add a replica region to the builder
    pub fn add_replica_region(mut self, region: &str) -> Self {
        self.replica_regions.push(region.to_string());
        self
    }
    
    /// Set the endpoint for all clients (used for testing)
    pub fn with_endpoint(mut self, endpoint: &str) -> Self {
        self.endpoint = Some(endpoint.to_string());
        self
    }
    
    /// Set the timeout for all clients
    pub fn with_timeout(mut self, timeout: Duration) -> Self {
        self.timeout = timeout;
        self
    }
    
    /// Build the DynamoDB clients
    pub async fn build(self) -> Result<(Arc<dyn DynamoDbClient>, Vec<Arc<dyn DynamoDbClient>>)> {
        // Create the primary client
        let primary = if let Some(endpoint) = &self.endpoint {
            StandardDynamoDbClient::with_endpoint(&self.primary_region, endpoint).await?
        } else {
            StandardDynamoDbClient::new(&self.primary_region).await?
        }.with_timeout(self.timeout);
        
        // Create the replica clients
        let mut replicas = Vec::new();
        for region in &self.replica_regions {
            let client = if let Some(endpoint) = &self.endpoint {
                StandardDynamoDbClient::with_endpoint(region, endpoint).await?
            } else {
                StandardDynamoDbClient::new(region).await?
            }.with_timeout(self.timeout);
            
            replicas.push(Arc::new(client) as Arc<dyn DynamoDbClient>);
        }
        
        Ok((Arc::new(primary) as Arc<dyn DynamoDbClient>, replicas))
    }
}