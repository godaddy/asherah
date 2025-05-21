//! MySQL metastore implementation
//!
//! This module provides an implementation of the `Metastore` trait using MySQL.

use crate::envelope::EnvelopeKeyRecord;
use crate::error::{Error, Result};
use crate::timer;
use crate::Metastore;
use async_trait::async_trait;
use chrono::{TimeZone, Utc};
use sqlx::{mysql::MySql, Pool};
use std::sync::Arc;

// SQL queries
const LOAD_KEY_QUERY: &str = "SELECT key_record FROM encryption_key WHERE id = ? AND created = ?";
const LOAD_LATEST_QUERY: &str =
    "SELECT key_record FROM encryption_key WHERE id = ? ORDER BY created DESC LIMIT 1";
const STORE_KEY_QUERY: &str =
    "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)";

/// MySQL metastore implementation
#[derive(Debug)]
pub struct MySqlMetastore {
    /// The MySQL connection pool
    pool: Arc<Pool<MySql>>,
}

impl MySqlMetastore {
    /// Creates a new MySQL metastore with the given connection pool
    pub fn new(pool: Arc<Pool<MySql>>) -> Self {
        Self { pool }
    }

    /// Parses envelope JSON from database
    fn parse_envelope(json_str: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json_str)
            .map_err(|e| Error::Metastore(format!("Error parsing envelope: {}", e)))
    }
}

#[async_trait]
impl Metastore for MySqlMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.mysql.load");

        let created_dt = Utc
            .timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;

        let result = sqlx::query_as::<MySql, (String,)>(LOAD_KEY_QUERY)
            .bind(id)
            .bind(created_dt)
            .fetch_optional(&*self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Error loading key: {}", e)))?;

        let envelope = match result {
            Some((json_str,)) => Some(Self::parse_envelope(&json_str)?),
            None => None,
        };

        Ok(envelope)
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.mysql.loadlatest");

        let result = sqlx::query_as::<MySql, (String,)>(LOAD_LATEST_QUERY)
            .bind(id)
            .fetch_optional(&*self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Error loading latest key: {}", e)))?;

        let envelope = match result {
            Some((json_str,)) => Some(Self::parse_envelope(&json_str)?),
            None => None,
        };

        Ok(envelope)
    }

    async fn store(&self, id: &str, created: i64, envelope: &EnvelopeKeyRecord) -> Result<bool> {
        let _timer = timer!("ael.metastore.mysql.store");

        let created_dt = Utc
            .timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;

        let json = serde_json::to_string(envelope)
            .map_err(|e| Error::Metastore(format!("Error serializing envelope: {}", e)))?;

        let result = sqlx::query(STORE_KEY_QUERY)
            .bind(id)
            .bind(created_dt)
            .bind(json)
            .execute(&*self.pool)
            .await
            .map_err(|e| Error::Metastore(format!("Error storing key: {}", e)))?;

        Ok(result.rows_affected() > 0)
    }
}
