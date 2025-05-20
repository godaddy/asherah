use anyhow::{anyhow, Result};
use asherah_appencryption::{
    crypto::aes256gcm::Aes256GcmCrypto,
    kms::static_kms::StaticKeyManagementServiceImpl,
    persistence::sql::SqlMetastoreImpl,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
    Config,
};
use mysql::{Conn, Opts, OptsBuilder};
use serde_json::{json, Value};
use std::{env, sync::Arc};

pub mod constants;
use constants::*;

pub struct TestContext {
    pub payload_string: Option<String>,
    pub encrypted_payload: Option<String>,
    pub decrypted_payload: Option<String>,
    pub connection: Option<Conn>,
}

impl TestContext {
    pub fn new() -> Self {
        Self {
            payload_string: None,
            encrypted_payload: None,
            decrypted_payload: None,
            connection: None,
        }
    }

    pub fn connect_sql(&mut self) -> Result<()> {
        // Get database connection parameters from environment variables with fallbacks
        let user = env::var("TEST_DB_USER").unwrap_or_else(|_| "test".to_string());
        let password = env::var("TEST_DB_PASSWORD").unwrap_or_else(|_| "test".to_string());
        let hostname = env::var("TEST_DB_HOSTNAME").unwrap_or_else(|_| "localhost".to_string());
        let port = env::var("TEST_DB_PORT").unwrap_or_else(|_| "3306".to_string());
        let db_name = env::var("TEST_DB_NAME").unwrap_or_else(|_| "test".to_string());
        
        // Format connection string
        let connection_string = format!(
            "mysql://{}:{}@{}:{}/{}",
            user, password, hostname, port, db_name
        );

        // Create MySQL connection options
        let opts = Opts::from_url(&connection_string)
            .map_err(|e| anyhow!("Failed to parse MySQL connection string: {}", e))?;
            
        let builder = OptsBuilder::from_opts(opts)
            .tcp_nodelay(Some(true));
        
        // Establish database connection
        self.connection = Some(Conn::new(builder)
            .map_err(|e| anyhow!("Failed to connect to MySQL database: {}", e))?);
            
        Ok(())
    }

    pub fn encrypt_data(&mut self) -> Result<()> {
        let payload_string = self.payload_string.as_ref().ok_or_else(|| anyhow!("No payload string found"))?;
        
        // Get connection
        let conn = self.connection.as_mut().ok_or_else(|| anyhow!("No database connection"))?;
        
        // Create crypto provider and KMS
        let crypto = Arc::new(Aes256GcmCrypto::default());
        let kms = StaticKeyManagementServiceImpl::new(KEY_MANAGEMENT_STATIC_MASTER_KEY.to_string(), crypto.clone())?;
        
        // Create metastore and policy with the same settings as other language implementations
        let metastore = SqlMetastoreImpl::new(conn);
        
        // Configure policy with explicit expiry and revoke check settings to match other implementations
        let policy = CryptoPolicy::builder()
            .key_expiry_days(KEY_EXPIRY_DAYS)
            .revoke_check_minutes(REVOKE_CHECK_MINUTES)
            .build();
        
        // Configure session factory
        let config = Config::new(
            DEFAULT_SERVICE_ID.to_string(),
            DEFAULT_PRODUCT_ID.to_string(),
            policy,
        );
        
        let factory = SessionFactory::new(config, Arc::new(metastore), Arc::new(kms), crypto);
        
        // Create session
        let mut session = factory.get_session(DEFAULT_PARTITION_ID.to_string())?;
        
        // Encrypt data
        let encrypted = session.encrypt(payload_string.as_bytes())?;
        
        // Convert to base64 JSON string
        let json_value = json!({
            "Data": encrypted.data,
            "KeyMeta": {
                "Created": encrypted.created.to_rfc3339(),
                "KeyId": encrypted.key_id
            }
        });
        
        let json_string = serde_json::to_string(&json_value)?;
        let base64_string = base64::encode(json_string);
        
        self.encrypted_payload = Some(base64_string);
        
        Ok(())
    }
    
    pub fn decrypt_data(&mut self) -> Result<()> {
        let encrypted_payload = self.encrypted_payload.as_ref().ok_or_else(|| anyhow!("No encrypted payload found"))?;
        
        // Get connection
        let conn = self.connection.as_mut().ok_or_else(|| anyhow!("No database connection"))?;
        
        // Create crypto provider and KMS
        let crypto = Arc::new(Aes256GcmCrypto::default());
        let kms = StaticKeyManagementServiceImpl::new(KEY_MANAGEMENT_STATIC_MASTER_KEY.to_string(), crypto.clone())?;
        
        // Create metastore and policy with the same settings as other language implementations
        let metastore = SqlMetastoreImpl::new(conn);
        
        // Configure policy with explicit expiry and revoke check settings to match other implementations
        let policy = CryptoPolicy::builder()
            .key_expiry_days(KEY_EXPIRY_DAYS)
            .revoke_check_minutes(REVOKE_CHECK_MINUTES)
            .build();
        
        // Configure session factory
        let config = Config::new(
            DEFAULT_SERVICE_ID.to_string(),
            DEFAULT_PRODUCT_ID.to_string(),
            policy,
        );
        
        let factory = SessionFactory::new(config, Arc::new(metastore), Arc::new(kms), crypto);
        
        // Create session
        let mut session = factory.get_session(DEFAULT_PARTITION_ID.to_string())?;
        
        // Decrypt data
        let base64_decoded = base64::decode(encrypted_payload)?;
        let json_string = String::from_utf8(base64_decoded)?;
        let json_value: Value = serde_json::from_str(&json_string)?;
        
        let data_row = asherah_appencryption::DataRowRecord {
            data: json_value["Data"].as_str().ok_or_else(|| anyhow!("Missing Data field"))?.to_string(),
            created: json_value["KeyMeta"]["Created"].as_str().ok_or_else(|| anyhow!("Missing Created field"))?.to_string(),
            key_id: json_value["KeyMeta"]["KeyId"].as_str().ok_or_else(|| anyhow!("Missing KeyId field"))?.to_string(),
        };
        
        let decrypted = session.decrypt(&data_row)?;
        self.decrypted_payload = Some(String::from_utf8(decrypted)?);
        
        Ok(())
    }
}