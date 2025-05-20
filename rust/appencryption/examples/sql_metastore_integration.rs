use appencryption::{
    kms::StaticKeyManagementService,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use securememory::protected_memory::DefaultSecretFactory;
use std::env;
use std::sync::Arc;

#[cfg(feature = "mysql")]
use appencryption::metastore::MySqlMetastore;

#[cfg(feature = "postgres")]
use appencryption::metastore::PostgresMetastore;

/// This example demonstrates how to integrate Asherah with SQL databases for key storage.
///
/// It shows:
/// 1. Using MySQL metastore implementation
/// 2. Using PostgreSQL metastore implementation
/// 3. Setting up proper error handling and connection pooling
/// 4. Encrypting and decrypting data using keys stored in SQL databases

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("SQL Metastore Integration Example");
    println!("=================================");

    // Choose which database to use based on environment variable
    let db_type = env::var("DB_TYPE").unwrap_or_else(|_| "mysql".to_string());

    match db_type.as_str() {
        #[cfg(feature = "mysql")]
        "mysql" => run_mysql_example().await,
        #[cfg(feature = "postgres")]
        "postgres" => run_postgres_example().await,
        _ => {
            #[cfg(all(not(feature = "mysql"), not(feature = "postgres")))]
            {
                eprintln!(
                    "No SQL features enabled. Enable either the 'mysql' or 'postgres' feature."
                );
                std::process::exit(1);
            }
            #[cfg(any(feature = "mysql", feature = "postgres"))]
            {
                eprintln!(
                    "Unknown database type: {}. Use DB_TYPE=mysql or DB_TYPE=postgres",
                    db_type
                );
                std::process::exit(1);
            }
        }
    }
}

#[cfg(feature = "mysql")]
async fn run_mysql_example() -> Result<(), Box<dyn std::error::Error>> {
    println!("Using MySQL metastore");

    // Get database URL from environment or use default
    let database_url = env::var("MYSQL_URL")
        .unwrap_or_else(|_| "mysql://root:password@localhost:3306/asherah".to_string());

    // Setup connection pool
    let pool = sqlx::mysql::MySqlPoolOptions::new()
        .max_connections(5)
        .connect(&database_url)
        .await?;

    // Ensure the encryption_key table exists
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS encryption_key (
            id VARCHAR(255) NOT NULL,
            created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            key_record TEXT NOT NULL,
            PRIMARY KEY (id, created),
            INDEX (created)
        )",
    )
    .execute(&pool)
    .await?;

    // Create session factory components
    let policy = CryptoPolicy::new();
    let master_key = vec![0u8; 32]; // In production, use a real master key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(MySqlMetastore::new(Arc::new(pool)));
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = SessionFactory::builder()
        .with_service("mysql_example")
        .with_product("sql_integration")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?;

    // Run encryption/decryption test
    run_encryption_test(&factory).await?;

    Ok(())
}

#[cfg(feature = "postgres")]
async fn run_postgres_example() -> Result<(), Box<dyn std::error::Error>> {
    println!("Using PostgreSQL metastore");

    // Get database URL from environment or use default
    let database_url = env::var("POSTGRES_URL")
        .unwrap_or_else(|_| "postgres://postgres:password@localhost:5432/asherah".to_string());

    // Setup connection pool
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(5)
        .connect(&database_url)
        .await?;

    // Ensure the encryption_key table exists
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS encryption_key (
            id VARCHAR(255) NOT NULL,
            created BIGINT NOT NULL,
            key_record TEXT NOT NULL,
            PRIMARY KEY (id, created)
        )",
    )
    .execute(&pool)
    .await?;

    // Create session factory components
    let policy = CryptoPolicy::new();
    let master_key = vec![0u8; 32]; // In production, use a real master key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(PostgresMetastore::new(Arc::new(pool)));
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = SessionFactory::builder()
        .with_service("postgres_example")
        .with_product("sql_integration")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?;

    // Run encryption/decryption test
    run_encryption_test(&factory).await?;

    Ok(())
}

async fn run_encryption_test(factory: &SessionFactory) -> Result<(), Box<dyn std::error::Error>> {
    // Create sessions for different partitions
    let session1 = factory.session("user123").await?;
    let session2 = factory.session("user456").await?;

    // Test data
    let data1 = b"Sensitive data for user 123";
    let data2 = b"Sensitive data for user 456";

    // Encrypt data with different sessions
    let encrypted1 = session1.encrypt(data1).await?;
    let encrypted2 = session2.encrypt(data2).await?;

    println!("Data encrypted successfully");
    println!("  User 123 record ID: {}", encrypted1.key.id);
    println!("  User 456 record ID: {}", encrypted2.key.id);

    // Decrypt data
    let decrypted1 = session1.decrypt(&encrypted1).await?;
    let decrypted2 = session2.decrypt(&encrypted2).await?;

    // Verify the data matches
    assert_eq!(data1, &decrypted1[..]);
    assert_eq!(data2, &decrypted2[..]);

    println!("Data decrypted successfully");

    // Test cross-partition decryption (should fail for security)
    match session1.decrypt(&encrypted2).await {
        Err(_) => println!("Cross-partition decryption correctly failed"),
        Ok(_) => panic!("Cross-partition decryption should have failed!"),
    }

    // Close sessions
    session1.close().await?;
    session2.close().await?;

    println!("\nSQL metastore integration test completed successfully!");

    Ok(())
}

#[cfg(not(feature = "mysql"))]
async fn run_mysql_example() -> Result<(), Box<dyn std::error::Error>> {
    eprintln!("MySQL support not enabled. Build with --features mysql");
    std::process::exit(1);
}

#[cfg(not(feature = "postgres"))]
async fn run_postgres_example() -> Result<(), Box<dyn std::error::Error>> {
    eprintln!("PostgreSQL support not enabled. Build with --features postgres");
    std::process::exit(1);
}
