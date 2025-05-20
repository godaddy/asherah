use appencryption::kms::StaticKeyManagementService;
use appencryption::metastore::InMemoryMetastore;
use appencryption::policy::CryptoPolicy;
use appencryption::session::{Session, SessionFactory};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Set up components
    let policy = CryptoPolicy::new();
    let kms = Arc::new(StaticKeyManagementService::new(vec![0u8; 32]));
    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory using builder pattern
    let factory = SessionFactory::builder()
        .with_service("myservice")
        .with_product("myproduct")
        .with_policy(policy)
        .with_kms(kms)
        .with_metastore(metastore)
        .with_secret_factory(secret_factory)
        .build()?;

    // Get a session
    let session = factory.session("user123").await?;

    // Encrypt some data
    let data = b"Hello, World!";
    let encrypted = session.encrypt(data).await?;
    println!("Encrypted {} bytes", encrypted.data.len());

    // Decrypt it back
    let decrypted = session.decrypt(&encrypted).await?;
    println!("Decrypted: {}", String::from_utf8_lossy(&decrypted));

    // Close the session
    session.close().await?;

    Ok(())
}
