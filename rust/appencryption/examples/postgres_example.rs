use appencryption::session::SessionFactory;
use appencryption::kms::StaticKeyManagementService;
#[cfg(feature = "postgres")]
use appencryption::metastore::PostgresMetastore;
use appencryption::policy::CryptoPolicy;
use securememory::protected_memory::DefaultSecretFactory;
#[cfg(feature = "postgres")]
use sqlx::postgres::PgPoolOptions;
use std::sync::Arc;
#[cfg(feature = "postgres")]
use std::env;

#[cfg(feature = "postgres")]
#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Get database URL from environment or use default
    let database_url = env::var("DATABASE_URL")
        .unwrap_or_else(|_| "postgres://postgres:postgres@localhost:5432/postgres".to_string());
        
    // Setup connection pool with connection timeout and max connections
    let pool = PgPoolOptions::new()
        .max_connections(5)
        .connect(&database_url)
        .await?;
        
    // Ensure the encryption_key table exists
    sqlx::query(
        "CREATE TABLE IF NOT EXISTS encryption_key (
            id VARCHAR(255) NOT NULL,
            created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            key_record TEXT NOT NULL,
            PRIMARY KEY (id, created)
        )"
    )
    .execute(&pool)
    .await?;
    
    // Create index on created column if it doesn't exist
    sqlx::query(
        "CREATE INDEX IF NOT EXISTS idx_encryption_key_created ON encryption_key (created)"
    )
    .execute(&pool)
    .await?;
    
    // Create dependencies for session factory
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24);
    let cache_max_age = TimeDelta::hours(2);
    let create_date_precision = TimeDelta::minutes(1);
    
    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap())
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_session_cache();
    
    let master_key = vec![0u8; 32]; // In production, use a real master key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    
    // Create PostgreSQL metastore
    let metastore = Arc::new(PostgresMetastore::new(Arc::new(pool)));
    
    // Create secret factory
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory
    let factory = Arc::new(SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?);
    
    // Create session for a partition
    let session = factory.session("user123").await?;
    
    // Encrypt data
    let data = b"secret data".to_vec();
    let encrypted = session.encrypt(&data).await?;
    println!("Encrypted data: {:?}", encrypted);
    
    // Decrypt data
    let decrypted = session.decrypt(&encrypted).await?;
    println!("Decrypted data: {:?}", String::from_utf8_lossy(&decrypted));
    
    // Close session when done
    session.close().await?;
    
    Ok(())
}

#[cfg(not(feature = "postgres"))]
fn main() {
    eprintln!("PostgreSQL feature not enabled. Build with --features postgres");
}
