//! Example demonstrating how to use the ADO metastore implementation
//! 
//! This example shows how to set up and use the ADO metastore, which provides
//! a generic database interface similar to ADO.NET in C#.
//! 
//! NOTE: As of this release, the ADO metastore is a placeholder implementation.
//! This example shows the intended usage pattern for when full implementation
//! is available in a future release.

use appencryption::kms::static_kms::StaticKeyManagementService;
use appencryption::metastore::AdoMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::{Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use log::{info, warn};
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Initialize logger for better output
    env_logger::init();
    
    info!("ADO.NET Metastore Example");
    info!("========================");
    
    warn!("This example uses the ADO metastore which is currently a placeholder implementation.");
    warn!("Full implementation will be available in a future release.");
    warn!("The code below shows the intended usage pattern, but operations will return NotImplemented errors.");
    
    // Example connection string for a Microsoft SQL Server database
    // In a real application, you would use your actual database connection info
    let connection_string = "Server=localhost;Database=asherah;User Id=sa;Password=SuperSecret123;";
    
    // Create the ADO metastore instance
    let metastore = Arc::new(AdoMetastore::new(connection_string.to_string(), "encryption_keys".to_string()));
    
    // In a fully implemented version, you would create tables if needed
    // This will currently return a NotImplemented error
    if let Err(e) = metastore.create_table_if_not_exists().await {
        warn!("Could not create tables: {}", e);
        warn!("This is expected with the placeholder implementation");
    }
    
    // Set up other components
    let kms = Arc::new(StaticKeyManagementService::new("static master key".to_string()));
    let policy = CryptoPolicy::new();
    let secret_factory = Arc::new(DefaultSecretFactory::new());
    
    // Create session factory
    let factory = SessionFactory::builder()
        .with_service("ado_example")
        .with_product("test_product")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?;
    
    info!("Creating session...");
    
    // This will fail since ADO metastore is not implemented yet,
    // but shows the correct usage pattern
    match factory.session("user123").await {
        Ok(session) => {
            // In a working implementation, these operations would succeed
            let data = b"This is some secret data to encrypt";
            let encrypted = session.encrypt(data).await?;
            info!("Data encrypted successfully");
            
            let decrypted = session.decrypt(&encrypted).await?;
            info!("Data decrypted successfully");
            
            assert_eq!(data.as_ref(), decrypted.as_slice());
            info!("Original and decrypted data match!");
            
            // Clean up resources
            session.close().await?;
        },
        Err(e) => {
            warn!("Session creation failed as expected: {}", e);
            warn!("The ADO metastore is a placeholder implementation.");
            warn!("Full implementation will be available in a future release.");
        }
    }
    
    info!("Example complete");
    Ok(())
}