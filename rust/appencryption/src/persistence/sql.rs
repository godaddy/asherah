use crate::envelope::EnvelopeKeyRecord;
use crate::error::{Error, Result};
use crate::Metastore;

use async_trait::async_trait;
use chrono::{DateTime, TimeZone, Utc};
use metrics::histogram;
use regex::Regex;
use std::sync::Arc;
use std::time::Instant;

// Default SQL queries for different operations
const DEFAULT_LOAD_KEY_QUERY: &str =
    "SELECT key_record FROM encryption_key WHERE id = ? AND created = ?";
const DEFAULT_STORE_KEY_QUERY: &str =
    "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)";
const DEFAULT_LOAD_LATEST_QUERY: &str =
    "SELECT key_record from encryption_key WHERE id = ? ORDER BY created DESC LIMIT 1";

/// Database type for SQL metastore
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SqlMetastoreDbType {
    /// MySQL database
    MySql,

    /// PostgreSQL database
    Postgres,

    /// Oracle database
    Oracle,

    /// SQL Server database
    SqlServer,
}

impl SqlMetastoreDbType {
    /// Converts SQL placeholders to the database-specific format
    fn convert_placeholders(&self, sql: &str) -> String {
        match self {
            SqlMetastoreDbType::MySql => sql.to_string(),
            SqlMetastoreDbType::Postgres => {
                // Convert ? to $1, $2, etc.
                let re = Regex::new(r"\?").expect("Failed to create regex pattern for SQL placeholders");
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures<'_>| {
                    counter += 1;
                    format!("${}", counter)
                })
                .to_string()
            }
            SqlMetastoreDbType::Oracle => {
                // Convert ? to :1, :2, etc.
                let re = Regex::new(r"\?").expect("Failed to create regex pattern for SQL placeholders");
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures<'_>| {
                    counter += 1;
                    format!(":{}", counter)
                })
                .to_string()
            }
            SqlMetastoreDbType::SqlServer => {
                // Convert ? to @p1, @p2, etc.
                let re = Regex::new(r"\?").expect("Failed to create regex pattern for SQL placeholders");
                let mut counter = 0;
                re.replace_all(sql, |_: &regex::Captures<'_>| {
                    counter += 1;
                    format!("@p{}", counter)
                })
                .to_string()
            }
        }
    }
}

/// SQL database client trait for metastore operations
#[async_trait]
pub trait SqlClient: Send + Sync + std::fmt::Debug {
    /// Loads a key record by ID and created timestamp
    async fn load_key(
        &self,
        query: &str,
        id: &str,
        created: DateTime<Utc>,
    ) -> Result<Option<String>>;

    /// Loads the latest key record by ID
    async fn load_latest_key(&self, query: &str, id: &str) -> Result<Option<String>>;

    /// Stores a key record
    async fn store_key(
        &self,
        query: &str,
        id: &str,
        created: DateTime<Utc>,
        key_record: &str,
    ) -> Result<bool>;
}

/// SQL metastore implementation
#[derive(Debug)]
pub struct SqlMetastore {
    /// SQL client
    client: Arc<dyn SqlClient>,

    /// Database type (for future use)
    #[allow(dead_code)]
    db_type: SqlMetastoreDbType,

    /// Query for loading a key
    load_key_query: String,

    /// Query for storing a key
    store_key_query: String,

    /// Query for loading the latest key
    load_latest_query: String,
}

impl SqlMetastore {
    /// Creates a new SQL metastore with the given client and options
    pub fn new(client: Arc<dyn SqlClient>, db_type: SqlMetastoreDbType) -> Self {
        let load_key_query = db_type.convert_placeholders(DEFAULT_LOAD_KEY_QUERY);
        let store_key_query = db_type.convert_placeholders(DEFAULT_STORE_KEY_QUERY);
        let load_latest_query = db_type.convert_placeholders(DEFAULT_LOAD_LATEST_QUERY);

        Self {
            client,
            db_type,
            load_key_query,
            store_key_query,
            load_latest_query,
        }
    }

    /// Creates a new SQL metastore with the given client and custom queries
    pub fn with_custom_queries(
        client: Arc<dyn SqlClient>,
        db_type: SqlMetastoreDbType,
        load_key_query: impl Into<String>,
        store_key_query: impl Into<String>,
        load_latest_query: impl Into<String>,
    ) -> Self {
        let load_key_query = db_type.convert_placeholders(&load_key_query.into());
        let store_key_query = db_type.convert_placeholders(&store_key_query.into());
        let load_latest_query = db_type.convert_placeholders(&load_latest_query.into());

        Self {
            client,
            db_type,
            load_key_query,
            store_key_query,
            load_latest_query,
        }
    }

    /// Parses an envelope key record from a JSON string
    fn parse_envelope(json_str: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json_str)
            .map_err(|e| Error::Metastore(format!("Unable to parse key: {}", e)))
    }
}

#[async_trait]
impl Metastore for SqlMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let start = Instant::now();

        let created_dt = Utc
            .timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;

        let result = match self
            .client
            .load_key(&self.load_key_query, id, created_dt)
            .await?
        {
            Some(json_str) => Some(Self::parse_envelope(&json_str)?),
            None => None,
        };

        histogram!("ael.metastore.sql.load", start.elapsed());

        Ok(result)
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let start = Instant::now();

        let result = match self
            .client
            .load_latest_key(&self.load_latest_query, id)
            .await?
        {
            Some(json_str) => Some(Self::parse_envelope(&json_str)?),
            None => None,
        };

        histogram!("ael.metastore.sql.loadlatest", start.elapsed());

        Ok(result)
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let start = Instant::now();

        let created_dt = Utc
            .timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;

        let json_str = serde_json::to_string(envelope)
            .map_err(|e| Error::Metastore(format!("Error marshaling envelope: {}", e)))?;

        let result = self
            .client
            .store_key(&self.store_key_query, id, created_dt, &json_str)
            .await;

        histogram!("ael.metastore.sql.store", start.elapsed());

        result
    }
}
