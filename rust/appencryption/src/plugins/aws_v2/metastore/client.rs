use crate::envelope::{EnvelopeKeyRecord, KeyMeta};
use crate::error::{Error, Result};
use crate::timer;
use crate::Metastore;
use async_trait::async_trait;
use base64::Engine;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

// Default table name for the DynamoDB metastore
const DEFAULT_TABLE_NAME: &str = "EncryptionKey";

// DynamoDB attribute names
pub const PARTITION_KEY: &str = "Id";
pub const SORT_KEY: &str = "Created";
pub const KEY_RECORD: &str = "KeyRecord";

/// DynamoDB client interface for metastore operations
#[async_trait]
pub trait DynamoDbClient: Send + Sync {
    /// Gets an item from DynamoDB
    async fn get_item(&self, table_name: &str, key: DynamoDbKey) -> Result<Option<DynamoDbItem>>;

    /// Puts an item in DynamoDB if it doesn't exist
    async fn put_item_if_not_exists(&self, table_name: &str, item: DynamoDbItem) -> Result<bool>;

    /// Queries DynamoDB for the latest item with the given partition key
    async fn query_latest(
        &self,
        table_name: &str,
        partition_key: &str,
    ) -> Result<Vec<DynamoDbItem>>;

    /// Returns the region for this client
    fn region(&self) -> &str;

    /// Returns whether this client is healthy
    fn is_healthy(&self) -> bool;

    /// Checks if the client is healthy by performing a lightweight operation
    async fn health_check(&self) -> Result<bool>;
}

/// DynamoDB key for get operations
#[derive(Debug, Clone)]
pub struct DynamoDbKey {
    /// Partition key
    pub id: String,

    /// Sort key
    pub created: i64,
}

/// DynamoDB item for store operations
#[derive(Debug, Clone)]
pub struct DynamoDbItem {
    /// Partition key
    pub id: String,

    /// Sort key
    pub created: i64,

    /// Key record
    pub key_record: DynamoDbEnvelope,
}

/// DynamoDB envelope for key records
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DynamoDbEnvelope {
    /// Whether the key is revoked
    #[serde(rename = "Revoked", skip_serializing_if = "Option::is_none")]
    pub revoked: Option<bool>,

    /// The creation timestamp of the key
    #[serde(rename = "Created")]
    pub created: i64,

    /// The encrypted key, base64 encoded
    #[serde(rename = "Key")]
    pub encrypted_key: String,

    /// The parent key metadata
    #[serde(rename = "ParentKeyMeta", skip_serializing_if = "Option::is_none")]
    pub parent_key_meta: Option<DynamoDbKeyMeta>,
}

/// DynamoDB key metadata
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DynamoDbKeyMeta {
    /// The key ID
    #[serde(rename = "KeyId")]
    pub id: String,

    /// The creation timestamp of the key
    #[serde(rename = "Created")]
    pub created: i64,
}

/// Client status for health tracking
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum ClientStatus {
    /// Client is healthy
    Healthy,

    /// Client is unhealthy
    Unhealthy,

    /// Client status is unknown
    Unknown,
}

/// Client health information
struct ClientHealth {
    /// Last known status
    status: ClientStatus,

    /// Last check time
    last_check: Instant,

    /// Failure count
    failure_count: usize,
}

impl ClientHealth {
    /// Create a new client health tracker
    fn new() -> Self {
        Self {
            status: ClientStatus::Unknown,
            last_check: Instant::now(),
            failure_count: 0,
        }
    }

    /// Update status based on health check result
    fn update(&mut self, healthy: bool) {
        self.last_check = Instant::now();

        if healthy {
            self.status = ClientStatus::Healthy;
            self.failure_count = 0;
        } else {
            self.status = ClientStatus::Unhealthy;
            self.failure_count += 1;
        }
    }

    /// Check if the client is healthy
    fn is_healthy(&self) -> bool {
        self.status == ClientStatus::Healthy || self.status == ClientStatus::Unknown
    }

    /// Check if health needs to be rechecked
    fn needs_recheck(&self, recheck_interval: Duration) -> bool {
        self.status == ClientStatus::Unknown || self.last_check.elapsed() > recheck_interval
    }
}

/// Multi-region DynamoDB client
struct MultiRegionClient {
    /// Primary region client
    primary: Arc<dyn DynamoDbClient>,

    /// Replica region clients
    replicas: Vec<Arc<dyn DynamoDbClient>>,

    /// Client health status
    health: RwLock<HashMap<String, ClientHealth>>,

    /// Interval for rechecking unhealthy clients
    recheck_interval: Duration,

    /// Maximum retries before giving up
    max_retries: usize,
}

impl MultiRegionClient {
    /// Create a new multi-region client
    fn new(
        primary: Arc<dyn DynamoDbClient>,
        replicas: Vec<Arc<dyn DynamoDbClient>>,
        recheck_interval: Duration,
        max_retries: usize,
    ) -> Self {
        let mut health = HashMap::new();

        // Initialize health for all clients
        health.insert(primary.region().to_string(), ClientHealth::new());

        for replica in &replicas {
            health.insert(replica.region().to_string(), ClientHealth::new());
        }

        Self {
            primary,
            replicas,
            health: RwLock::new(health),
            recheck_interval,
            max_retries,
        }
    }

    /// Get all available clients
    fn all_clients(&self) -> Vec<Arc<dyn DynamoDbClient>> {
        let mut clients = Vec::with_capacity(self.replicas.len() + 1);
        clients.push(self.primary.clone());
        clients.extend(self.replicas.iter().cloned());
        clients
    }

    /// Get a list of healthy clients, preferring the primary
    fn healthy_clients(&self) -> Vec<Arc<dyn DynamoDbClient>> {
        let health = self.health.read().expect("Failed to read health lock");

        let mut clients = Vec::new();

        // First try primary if healthy
        if health
            .get(self.primary.region())
            .map(|h| h.is_healthy())
            .unwrap_or(true)
        {
            clients.push(self.primary.clone());
        }

        // Then add healthy replicas
        for replica in &self.replicas {
            if health
                .get(replica.region())
                .map(|h| h.is_healthy())
                .unwrap_or(true)
            {
                clients.push(replica.clone());
            }
        }

        clients
    }

    /// Update client health status
    async fn update_health(&self, client: &Arc<dyn DynamoDbClient>, healthy: bool) {
        let mut health = self.health.write().expect("Failed to acquire write lock on health");

        if let Some(client_health) = health.get_mut(client.region()) {
            client_health.update(healthy);
        }
    }

    /// Check if a client needs a health recheck
    fn needs_recheck(&self, client: &Arc<dyn DynamoDbClient>) -> bool {
        let health = self.health.read().expect("Failed to read health lock");

        health
            .get(client.region())
            .map(|h| h.needs_recheck(self.recheck_interval))
            .unwrap_or(true)
    }

    /// Perform a health check on a client
    async fn check_health(&self, client: &Arc<dyn DynamoDbClient>) -> bool {
        // Only check if enough time has passed since last check
        if !self.needs_recheck(client) {
            let health = self.health.read().expect("Failed to read health lock");
            return health
                .get(client.region())
                .map(|h| h.is_healthy())
                .unwrap_or(true);
        }

        match client.health_check().await {
            Ok(true) => {
                self.update_health(client, true).await;
                true
            }
            _ => {
                self.update_health(client, false).await;
                false
            }
        }
    }

    /// Execute an async operation with failover
    async fn execute_async<F, Fut, R>(&self, operation: F) -> Result<R>
    where
        F: Fn(&Arc<dyn DynamoDbClient>) -> Fut + Send + Sync,
        Fut: std::future::Future<Output = Result<R>> + Send,
    {
        let mut retries = 0;
        let mut last_error = None;

        while retries < self.max_retries {
            // First try healthy clients
            let clients = self.healthy_clients();

            if !clients.is_empty() {
                // Randomize order to distribute load
                use rand::seq::SliceRandom;
                let mut clients_copy = clients.clone();
                {
                    let mut rng = rand::thread_rng();
                    clients_copy.shuffle(&mut rng);
                }

                for client in clients_copy {
                    match operation(&client).await {
                        Ok(result) => return Ok(result),
                        Err(e) => {
                            // Mark client as unhealthy if operation failed
                            self.update_health(&client, false).await;

                            // Save the error
                            last_error = Some(e);

                            // Continue trying other clients
                            continue;
                        }
                    }
                }
            }

            // All healthy clients failed or no healthy clients, try rechecking all clients
            let all_clients = self.all_clients();

            for client in all_clients {
                if self.check_health(&client).await {
                    match operation(&client).await {
                        Ok(result) => return Ok(result),
                        Err(e) => {
                            last_error = Some(e);
                            continue;
                        }
                    }
                }
            }

            // All clients failed, increment retry counter
            retries += 1;

            // Wait a bit before retrying
            tokio::time::sleep(Duration::from_millis(100 * (2_u64.pow(retries as u32)))).await;
        }

        Err(last_error.unwrap_or_else(|| {
            Error::Metastore("All DynamoDB clients failed after retries".into())
        }))
    }
}

/// DynamoDB metastore implementation with global table support
pub struct DynamoDbMetastore {
    /// Multi-region DynamoDB client
    client: MultiRegionClient,

    /// Table name for the metastore
    table_name: String,

    /// Region suffix for global tables
    region_suffix: Option<String>,

    /// Whether to prefer the preferred region
    prefer_region: bool,
}

impl DynamoDbMetastore {
    /// Creates a new DynamoDbMetastore with a single client
    pub fn new(
        client: Arc<dyn DynamoDbClient>,
        table_name: Option<String>,
        use_region_suffix: bool,
    ) -> Self {
        Self::with_replicas(client, Vec::new(), table_name, use_region_suffix, false)
    }

    /// Creates a new DynamoDbMetastore with multiple region clients for global tables
    pub fn with_replicas(
        primary: Arc<dyn DynamoDbClient>,
        replicas: Vec<Arc<dyn DynamoDbClient>>,
        table_name: Option<String>,
        use_region_suffix: bool,
        prefer_region: bool,
    ) -> Self {
        let table_name = table_name.unwrap_or_else(|| DEFAULT_TABLE_NAME.to_string());

        let region_suffix = if use_region_suffix {
            Some(primary.region().to_string())
        } else {
            None
        };

        let client = MultiRegionClient::new(
            primary,
            replicas,
            Duration::from_secs(30), // Recheck unhealthy clients every 30 seconds
            3,                       // Maximum 3 retries
        );

        Self {
            client,
            table_name,
            region_suffix,
            prefer_region,
        }
    }

    /// Returns the primary client for this metastore
    pub fn primary_client(&self) -> &Arc<dyn DynamoDbClient> {
        &self.client.primary
    }

    /// Returns all clients for this metastore
    pub fn all_clients(&self) -> Vec<Arc<dyn DynamoDbClient>> {
        self.client.all_clients()
    }

    /// Returns the table name for this metastore
    pub fn table_name(&self) -> &str {
        &self.table_name
    }

    /// Returns the region suffix for this metastore
    pub fn region_suffix(&self) -> Option<&str> {
        self.region_suffix.as_deref()
    }

    /// Returns whether to prefer the region
    pub fn prefer_region(&self) -> bool {
        self.prefer_region
    }

    /// Gets the region-specific ID for a global table
    fn get_id_with_suffix(&self, id: &str) -> String {
        if let Some(suffix) = &self.region_suffix {
            if self.prefer_region {
                format!("{}_{}", id, suffix)
            } else {
                id.to_string()
            }
        } else {
            id.to_string()
        }
    }

    /// Converts a DynamoDB item to an EnvelopeKeyRecord
    fn decode_item(&self, item: DynamoDbItem) -> Result<EnvelopeKeyRecord> {
        // Create the key record
        let mut ekr = EnvelopeKeyRecord {
            revoked: item.key_record.revoked,
            id: item.id,
            created: item.key_record.created,
            encrypted_key: base64::engine::general_purpose::STANDARD
                .decode(&item.key_record.encrypted_key)
                .map_err(|e| Error::Metastore(format!("Failed to decode encrypted key: {}", e)))?,
            parent_key_meta: None,
        };

        // Add the parent key metadata if present
        if let Some(km) = item.key_record.parent_key_meta {
            ekr.parent_key_meta = Some(KeyMeta {
                id: km.id,
                created: km.created,
            });
        }

        Ok(ekr)
    }
}

#[async_trait]
impl Metastore for DynamoDbMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = crate::timer!("ael.metastore.dynamodb.load");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Create the key
        let key = DynamoDbKey {
            id: id_with_suffix,
            created,
        };

        // Execute the operation with failover
        let table = self.table_name.clone();
        let key = key.clone();
        let item = self
            .client
            .execute_async(move |client| {
                let client = client.clone();
                let table = table.clone();
                let key = key.clone();
                async move { client.get_item(&table, key).await }
            })
            .await?;

        // Decode the item if it exists
        let result = if let Some(item) = item {
            Some(self.decode_item(item)?)
        } else {
            None
        };

        drop(_timer);

        Ok(result)
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.dynamodb.loadlatest");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Execute the operation with failover
        let table = self.table_name.clone();
        let id = id_with_suffix.clone();
        let items = self
            .client
            .execute_async(move |client| {
                let client = client.clone();
                let table = table.clone();
                let id = id.clone();
                async move { client.query_latest(&table, &id).await }
            })
            .await?;

        // Decode the item if it exists
        let result = if let Some(item) = items.into_iter().next() {
            Some(self.decode_item(item)?)
        } else {
            None
        };

        drop(_timer);

        Ok(result)
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let _timer = timer!("ael.metastore.dynamodb.store");

        // Apply region suffix if needed
        let id_with_suffix = self.get_id_with_suffix(id);

        // Create the key metadata
        let parent_key_meta = envelope.parent_key_meta.as_ref().map(|km| DynamoDbKeyMeta {
            id: km.id.clone(),
            created: km.created,
        });

        // Create the envelope
        let db_envelope = DynamoDbEnvelope {
            revoked: envelope.revoked,
            created: envelope.created,
            encrypted_key: base64::engine::general_purpose::STANDARD
                .encode(&envelope.encrypted_key),
            parent_key_meta,
        };

        // Create the item
        let item = DynamoDbItem {
            id: id_with_suffix,
            created,
            key_record: db_envelope,
        };

        // Execute the operation with failover
        let table = self.table_name.clone();
        let item = item.clone();
        let result = self
            .client
            .execute_async(move |client| {
                let client = client.clone();
                let table = table.clone();
                let item = item.clone();
                async move { client.put_item_if_not_exists(&table, item).await }
            })
            .await;

        drop(_timer);

        result
    }
}
