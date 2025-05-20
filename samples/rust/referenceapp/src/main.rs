use appencryption::crypto::aes256gcm::Aes256GcmCrypto;
use appencryption::kms::{StaticKeyManagementService, aws::AwsKmsClientBuilder};
use appencryption::metastore::{DynamoDbMetastore, InMemoryMetastore, MySqlMetastore, PostgresMetastore};
use appencryption::DataRowRecord;
use appencryption::policy::CryptoPolicyBuilder;
use appencryption::session::SessionFactory;
use securememory::protected_memory::DefaultSecretFactory;

use std::sync::Arc;
use std::collections::HashMap;
use tokio::time::Duration;
use clap::{Parser, ValueEnum};
use anyhow::{Context, Result};
use base64::{Engine as _, engine::general_purpose::STANDARD as BASE64};
use sqlx::mysql::MySqlPoolOptions;
use sqlx::postgres::PgPoolOptions;

/// Command line options for the reference app
#[derive(Parser, Debug)]
#[command(author, version, about, long_about = None)]
struct Options {
    /// DRR to be decrypted (base64 encoded)
    #[arg(short = 'd', long = "drr-to-decrypt")]
    drr: Option<String>,

    /// Payload to be encrypted
    #[arg(short = 'p', long = "payload-to-encrypt")]
    payload: Option<String>,

    /// Configure what metastore to use
    #[arg(short = 'm', long = "metastore", value_enum, default_value_t = MetastoreType::Memory)]
    metastore: MetastoreType,

    /// Enables regional suffixes for DynamoDB (only supported by DynamoDB)
    #[arg(short = 'x', long = "enable-region-suffix")]
    enable_region_suffix: bool,

    /// Enables verbose output
    #[arg(short = 'v', long = "verbose")]
    verbose: bool,

    /// MySQL connection string
    #[arg(short = 'c', long = "conn")]
    connection_string: Option<String>,

    /// An optional endpoint URL for DynamoDB
    #[arg(long = "dynamodb-endpoint")]
    dynamodb_endpoint: Option<String>,

    /// The AWS region for DynamoDB requests
    #[arg(long = "dynamodb-region", default_value = "us-west-2")]
    dynamodb_region: String,

    /// The table name for DynamoDB
    #[arg(long = "dynamodb-table-name", default_value = "encryption_key")]
    dynamodb_table_name: String,

    /// Type of key management service to use
    #[arg(long = "kms-type", value_enum, default_value_t = KmsType::Static)]
    kms_type: KmsType,

    /// Preferred region to use for KMS if using AWS KMS
    #[arg(long = "preferred-region")]
    preferred_region: Option<String>,

    /// Comma separated list of <region>=<kms_arn> tuples
    #[arg(long = "region-arn-tuples")]
    region_tuples: Option<String>,

    /// The partition id to use for client sessions
    #[arg(long = "partition-id", default_value = "shopper123")]
    partition_id: String,
}

#[derive(Copy, Clone, PartialEq, Eq, ValueEnum, Debug)]
enum MetastoreType {
    /// In-memory metastore (for testing)
    Memory,
    /// Relational database metastore (MySQL)
    Rdbms,
    /// DynamoDB metastore
    Dynamodb,
}

#[derive(Copy, Clone, PartialEq, Eq, ValueEnum, Debug)]
enum KmsType {
    /// Static KMS (for testing)
    Static,
    /// AWS KMS
    Aws,
}

/// Create a metastore based on the provided options
async fn create_metastore(opts: &Options) -> Result<Arc<dyn appencryption::Metastore>> {
    match opts.metastore {
        MetastoreType::Rdbms => {
            if opts.verbose {
                println!("Using SQL metastore");
            }

            let conn_str = opts.connection_string.as_ref()
                .ok_or_else(|| anyhow::anyhow!("Connection string is mandatory with Metastore Type: SQL"))?;

            if conn_str.starts_with("mysql://") {
                let pool = MySqlPoolOptions::new()
                    .max_connections(5)
                    .connect(conn_str)
                    .await
                    .context("Failed to connect to MySQL")?;

                // Create the table if it doesn't exist
                sqlx::query(
                    "CREATE TABLE IF NOT EXISTS encryption_key (
                        id VARCHAR(255) NOT NULL,
                        created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        key_record TEXT NOT NULL,
                        PRIMARY KEY (id, created),
                        INDEX (created)
                    )"
                )
                .execute(&pool)
                .await
                .context("Failed to create encryption_key table")?;

                Ok(Arc::new(MySqlMetastore::new(Arc::new(pool))))
            } else if conn_str.starts_with("postgres://") {
                let pool = PgPoolOptions::new()
                    .max_connections(5)
                    .connect(conn_str)
                    .await
                    .context("Failed to connect to PostgreSQL")?;

                // Create the table if it doesn't exist
                sqlx::query(
                    "CREATE TABLE IF NOT EXISTS encryption_key (
                        id VARCHAR(255) NOT NULL,
                        created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        key_record TEXT NOT NULL,
                        PRIMARY KEY (id, created)
                    )"
                )
                .execute(&pool)
                .await
                .context("Failed to create encryption_key table")?;

                Ok(Arc::new(PostgresMetastore::new(Arc::new(pool))))
            } else {
                anyhow::bail!("Unsupported database connection string. Must start with mysql:// or postgres://");
            }
        },
        MetastoreType::Dynamodb => {
            if opts.verbose {
                println!("Using DynamoDB metastore");
            }

            let mut config_loader = aws_config::from_env();
            
            if let Some(endpoint) = &opts.dynamodb_endpoint {
                config_loader = config_loader.endpoint_url(endpoint);
            }

            config_loader = config_loader.region(aws_sdk_dynamodb::config::Region::new(opts.dynamodb_region.clone()));
            
            let config = config_loader.load().await;
            let client = aws_sdk_dynamodb::Client::new(&config);

            let mut builder = appencryption::metastore::DynamoDbMetastoreBuilder::new(&client, &opts.dynamodb_table_name);
            
            if opts.enable_region_suffix {
                builder = builder.with_region_suffix();
            }

            let metastore = builder.build().await?;
            Ok(Arc::new(metastore))
        },
        _ => {
            if opts.verbose {
                println!("Using in-memory metastore");
            }
            Ok(Arc::new(InMemoryMetastore::new()))
        }
    }
}

/// Create a KMS based on the provided options
async fn create_kms(opts: &Options, crypto: Arc<dyn appencryption::Crypto>) -> Result<Arc<dyn appencryption::KeyManagementService>> {
    match opts.kms_type {
        KmsType::Aws => {
            if opts.verbose {
                println!("Using AWS KMS");
            }

            // Check required parameters
            let preferred_region = opts.preferred_region.as_ref()
                .ok_or_else(|| anyhow::anyhow!("Preferred region is mandatory with KMS Type: AWS"))?;
            
            let region_tuples = opts.region_tuples.as_ref()
                .ok_or_else(|| anyhow::anyhow!("Region ARN tuples are mandatory with KMS Type: AWS"))?;

            // Parse region ARN map
            let mut region_arn_map = HashMap::new();
            for region_arn in region_tuples.split(',') {
                let parts: Vec<&str> = region_arn.split('=').collect();
                if parts.len() != 2 {
                    anyhow::bail!("Invalid region ARN tuple: {}", region_arn);
                }
                region_arn_map.insert(parts[0].to_string(), parts[1].to_string());
            }

            // Build KMS client
            let mut builder = AwsKmsClientBuilder::new();
            for (region, arn) in &region_arn_map {
                builder = builder.add_key_region(region, arn);
            }
            builder = builder.with_preferred_region(preferred_region);

            let kms = builder.build().await?;
            Ok(Arc::new(kms))
        },
        _ => {
            if opts.verbose {
                println!("Using static KMS");
            }
            
            // Use a fixed key for testing
            let key_bytes = b"thisIsAStaticMasterKeyForTesting";
            let key = key_bytes[0..32].to_vec();
            Ok(Arc::new(StaticKeyManagementService::new(key)))
        }
    }
}

/// Initialize logging based on verbosity
fn init_logging(verbose: bool) {
    let level = if verbose {
        tracing::Level::DEBUG
    } else {
        tracing::Level::INFO
    };

    tracing_subscriber::fmt()
        .with_max_level(level)
        .init();
}

#[tokio::main]
async fn main() -> Result<()> {
    // Parse command line options
    let opts = Options::parse();
    
    // Initialize logging
    init_logging(opts.verbose);

    // Validate options
    if opts.payload.is_some() && opts.drr.is_some() {
        anyhow::bail!("Either payload or drr can be provided, not both");
    }

    // Create crypto implementation
    let crypto = Arc::new(Aes256GcmCrypto::default());

    // Create policy
    let policy = CryptoPolicyBuilder::new().build();

    // Create KMS
    let kms = create_kms(&opts, crypto.clone()).await?;

    // Create metastore
    let metastore = create_metastore(&opts).await?;

    // Create secret factory
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = SessionFactory::new(
        "reference_app",
        "productId",
        policy,
        kms,
        metastore,
        secret_factory.clone(),
    );

    // Create a session
    let session = factory.session(&opts.partition_id).await?;

    // Determine payload
    let payload = match &opts.payload {
        Some(p) => p.clone(),
        None => "mysupersecretpayload".to_string(),
    };

    // Process based on options
    if let Some(drr_str) = &opts.drr {
        // Decrypt data row record
        let drr_bytes = BASE64.decode(drr_str)
            .context("Failed to decode base64 DRR")?;
        
        let data_row: DataRowRecord = serde_json::from_slice(&drr_bytes)
            .context("Failed to parse DRR JSON")?;

        // Decrypt the payload
        let data = session.decrypt(&data_row).await
            .context("Failed to decrypt data")?;

        println!("\ndecrypted value = {}", String::from_utf8_lossy(&data));
    } else {
        // Encrypt the payload
        let data_row = session.encrypt(payload.as_bytes()).await
            .context("Failed to encrypt data")?;

        // Serialize and encode the DRR
        let drr_bytes = serde_json::to_vec(&data_row)
            .context("Failed to serialize DRR")?;
        let drr_string = BASE64.encode(drr_bytes);

        println!("\ndata row record as string: {}", drr_string);

        // Decrypt to verify
        let data = session.decrypt(&data_row).await
            .context("Failed to decrypt data")?;

        println!("\ndecrypted value = {}", String::from_utf8_lossy(&data));
        println!("\nmatches = {}", String::from_utf8_lossy(&data) == payload);
    }

    // Clean up
    session.close().await?;
    
    Ok(())
}