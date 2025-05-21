#![allow(clippy::unseparated_literal_suffix, unused_variables)]

use appencryption::{
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;

/// This example demonstrates different caching behaviors in Asherah.
///
/// It shows:
/// 1. Key caching behavior with CryptoPolicy
/// 2. How caching improves performance
/// 3. Different cache configurations

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Advanced Caching Strategies Example");
    println!("==================================");

    // Create dependencies
    let master_key = vec![0_u8; 32]; // In a real app, use a secure key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create policies with different caching strategies
    let default_policy = CryptoPolicy::new();

    // Create a policy with SLRU caching
    let slru_policy = CryptoPolicy::new()
        .with_system_key_cache_policy("slru")
        .with_intermediate_key_cache_policy("slru");

    // Create a policy with simple caching (no eviction)
    let simple_policy = CryptoPolicy::new()
        .with_system_key_cache_policy("simple")
        .with_intermediate_key_cache_policy("simple");

    println!("\nUsing default CryptoPolicy:");
    let default_factory = SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(default_policy)
        .with_kms(kms.clone())
        .with_metastore(metastore.clone())
        .with_secret_factory(secret_factory.clone())
        .build()?;

    // Demonstrate key caching with default settings
    let session1 = default_factory.session("user1").await?;

    // First operation - creates keys
    let data = b"Sensitive data for user1";
    let encrypted1 = session1.encrypt(data).await?;
    println!("First encryption: created new keys");

    // Second operation - should use cached keys
    let encrypted2 = session1.encrypt(data).await?;
    println!("Second encryption: used cached keys");

    // Decrypt to verify
    let decrypted1 = session1.decrypt(&encrypted1).await?;
    let decrypted2 = session1.decrypt(&encrypted2).await?;
    assert_eq!(data, &decrypted1[..]);
    assert_eq!(data, &decrypted2[..]);

    session1.close().await?;

    // Create another session for the same partition
    println!("\nUsing a new session for the same partition:");
    let session2 = default_factory.session("user1").await?;

    // This should benefit from cached keys if within cache time
    let encrypted3 = session2.encrypt(data).await?;
    println!("Encryption with new session: benefited from factory-level cache");

    let decrypted3 = session2.decrypt(&encrypted3).await?;
    assert_eq!(data, &decrypted3[..]);

    session2.close().await?;

    // Different partition
    println!("\nUsing a different partition:");
    let session3 = default_factory.session("user2").await?;

    // This will create new keys for the new partition
    let data2 = b"Data for user2";
    let encrypted4 = session3.encrypt(data2).await?;
    println!("Encryption for new partition: created new keys");

    let decrypted4 = session3.decrypt(&encrypted4).await?;
    assert_eq!(data2, &decrypted4[..]);

    session3.close().await?;

    println!("\nKey Caching Summary:");
    println!("  - Keys are cached within sessions for improved performance");
    println!("  - The factory maintains a shared cache across sessions");
    println!("  - Different partitions use separate keys");
    println!("  - Cache expiration is governed by CryptoPolicy settings");

    // Demonstrate SLRU caching policy
    println!("\nUsing SLRU (Segmented LRU) cache policy:");
    let slru_factory = SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(slru_policy)
        .with_kms(kms.clone())
        .with_metastore(metastore.clone())
        .with_secret_factory(secret_factory.clone())
        .build()?;

    let slru_session = slru_factory.session("user1").await?;
    let encrypted_slru = slru_session.encrypt(data).await?;
    println!("Encryption with SLRU caching: created or reused keys");
    slru_session.close().await?;

    // Demonstrate Simple caching policy
    println!("\nUsing Simple cache policy (no eviction):");
    let simple_factory = SessionFactory::builder()
        .with_service("service")
        .with_product("product")
        .with_policy(simple_policy)
        .with_kms(kms.clone())
        .with_metastore(metastore.clone())
        .with_secret_factory(secret_factory.clone())
        .build()?;

    let simple_session = simple_factory.session("user1").await?;
    let encrypted_simple = simple_session.encrypt(data).await?;
    println!("Encryption with Simple caching: created or reused keys");
    simple_session.close().await?;

    println!("\nCache Policy Summary:");
    println!("  - Default: Uses LRU caching with size limits");
    println!("  - SLRU: Segmented LRU provides better protection against cache pollution");
    println!("  - Simple: No eviction policy, suitable for small key sets");

    println!("\nAll caching demonstrations completed successfully!");

    Ok(())
}
