//! PostgreSQL metastore implementation
//!
//! This module provides an implementation of the `Metastore` trait using PostgreSQL.

use crate::envelope::EnvelopeKeyRecord;
use crate::error::{Error, Result};
use crate::timer;
use crate::Metastore;
use async_trait::async_trait;
use sqlx::{postgres::Postgres, Pool};
use chrono::{TimeZone, Utc};
use std::sync::Arc;

// SQL queries
const LOAD_KEY_QUERY: &str = "SELECT key_record FROM encryption_key WHERE id = $1 AND created = $2";
const LOAD_LATEST_QUERY: &str = "SELECT key_record FROM encryption_key WHERE id = $1 ORDER BY created DESC LIMIT 1";
const STORE_KEY_QUERY: &str = "INSERT INTO encryption_key (id, created, key_record) VALUES ($1, $2, $3)";

/// PostgreSQL metastore implementation
pub struct PostgresMetastore {
    /// The PostgreSQL connection pool
    pool: Arc<Pool<Postgres>>,
}

impl PostgresMetastore {
    /// Creates a new PostgreSQL metastore with the given connection pool
    pub fn new(pool: Arc<Pool<Postgres>>) -> Self {
        Self { pool }
    }
    
    /// Parses envelope JSON from database
    fn parse_envelope(json_str: &str) -> Result<EnvelopeKeyRecord> {
        serde_json::from_str(json_str)
            .map_err(|e| Error::Metastore(format!("Error parsing envelope: {}", e)))
    }
}

#[async_trait]
impl Metastore for PostgresMetastore {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>> {
        let _timer = timer!("ael.metastore.postgres.load");
        
        let created_dt = Utc.timestamp_opt(created, 0)
            .single()
            .ok_or_else(|| Error::Metastore("Invalid timestamp".to_string()))?;
            
        let result = sqlx::query_as::<Postgres, (String,)>(LOAD_KEY_QUERY)
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
        let _timer = timer!("ael.metastore.postgres.loadlatest");
        
        let result = sqlx::query_as::<Postgres, (String,)>(LOAD_LATEST_QUERY)
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
        let _timer = timer!("ael.metastore.postgres.store");
        
        let created_dt = Utc.timestamp_opt(created, 0)
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