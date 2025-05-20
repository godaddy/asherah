use appencryption::session::{Session, SessionFactory};
use appencryption::kms::StaticKeyManagementService;
use appencryption::metastore::InMemoryMetastore;
use appencryption::policy::CryptoPolicy;
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

// Example showing how to configure a SessionFactory with advanced options
#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Advanced SessionFactory Example");
    
    // Create KMS with a static key (only for demonstration)
    let master_key = vec![0u8; 32];
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    
    // Create in-memory metastore
    let metastore = Arc::new(InMemoryMetastore::new());
    
    // Create a crypto policy with specific caching options
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24); // Keys expire after 24 hours
    let cache_max_age = TimeDelta::hours(2); // Cache entries expire after 2 hours
    let create_date_precision = TimeDelta::minutes(1); // Truncate timestamps to nearest minute
    
    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap());
    
    // Create secret factory
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create the SessionFactory
    let factory = Arc::new(SessionFactory::new(
        "my-service",
        "my-product",
        policy,
        kms,
        metastore,
        secret_factory.clone(),
        vec![], // no options
    ));
    
    // Create a session for a user
    let session = factory.session("user123").await?;
    
    println!("Created session for user123");
    
    // Data to encrypt
    let data = b"This is sensitive data!".to_vec();
    
    // Encrypt the data
    let encrypted_data = session.encrypt(&data).await?;
    println!("Encrypted data with key: {}", encrypted_data.key.id);
    
    // Decrypt the data
    let decrypted = session.decrypt(&encrypted_data).await?;
    let decrypted_str = String::from_utf8_lossy(&decrypted);
    println!("Decrypted data: {}", decrypted_str);
    
    // Create another session for the same user
    let session2 = factory.session("user123").await?;
    println!("Created second session for user123");
    
    // Decrypt with the second session
    let decrypted2 = session2.decrypt(&encrypted_data).await?;
    let decrypted_str2 = String::from_utf8_lossy(&decrypted2);
    println!("Decrypted with second session: {}", decrypted_str2);
    
    // Create a session with a different partition ID
    let partition_id = "user123_us-west-2";
    let suffix_session = factory.session(partition_id).await?;
    println!("Created session with partition: {}", partition_id);
    
    // Close sessions when done
    session.close().await?;
    session2.close().await?;
    suffix_session.close().await?;
    
    println!("Closed all sessions");
    
    Ok(())
}