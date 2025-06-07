use appencryption::{
    kms::static_kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    persistence::Persistence,
    session::SessionFactory,
    Partition,
};
use securememory::protected_memory::DefaultSecretFactory;
use std::sync::Arc;
use std::fs;
use std::path::PathBuf;

#[tokio::test]
async fn test_decrypt_data_from_other_languages() {
    // Create test components
    let kms = Arc::new(StaticKeyManagementService::new("static-master-key-for-testing".to_string()));
    let metastore = Arc::new(InMemoryMetastore::new());
    let persistence = Persistence::new(metastore, kms);
    let session_factory = SessionFactory::new(persistence, None);
    
    // Create partition with same identifiers used in other language tests
    let partition = Partition::new("service", "product");
    let session = session_factory.get_session(&partition).await.expect("Failed to create session");
    
    // Test data paths
    let encrypted_files_dir = PathBuf::from("../test-data");
    
    // Test decrypting data from Go implementation
    let go_file = encrypted_files_dir.join("go-encrypted.bin");
    if go_file.exists() {
        let encrypted_data = fs::read(go_file).expect("Failed to read Go encrypted file");
        let decrypted = session.decrypt(&encrypted_data).await.expect("Failed to decrypt Go data");
        
        // Verify the decrypted data (should be "test-data" in all implementations)
        assert_eq!(&decrypted, b"test-data", "Decrypted Go data does not match expected value");
        println!("Successfully decrypted data from Go implementation!");
    } else {
        println!("Skipping Go compatibility test - no encrypted file found");
    }
    
    // Test decrypting data from Java implementation
    let java_file = encrypted_files_dir.join("java-encrypted.bin");
    if java_file.exists() {
        let encrypted_data = fs::read(java_file).expect("Failed to read Java encrypted file");
        let decrypted = session.decrypt(&encrypted_data).await.expect("Failed to decrypt Java data");
        
        // Verify the decrypted data
        assert_eq!(&decrypted, b"test-data", "Decrypted Java data does not match expected value");
        println!("Successfully decrypted data from Java implementation!");
    } else {
        println!("Skipping Java compatibility test - no encrypted file found");
    }
    
    // Test decrypting data from C# implementation
    let csharp_file = encrypted_files_dir.join("csharp-encrypted.bin");
    if csharp_file.exists() {
        let encrypted_data = fs::read(csharp_file).expect("Failed to read C# encrypted file");
        let decrypted = session.decrypt(&encrypted_data).await.expect("Failed to decrypt C# data");
        
        // Verify the decrypted data
        assert_eq!(&decrypted, b"test-data", "Decrypted C# data does not match expected value");
        println!("Successfully decrypted data from C# implementation!");
    } else {
        println!("Skipping C# compatibility test - no encrypted file found");
    }
    
    // Create our own encrypted data for other implementations to decrypt
    let plain_data = b"test-data";
    let encrypted = session.encrypt(plain_data).await.expect("Failed to encrypt data");
    
    // Save the encrypted data for other implementations to use
    let rust_file = encrypted_files_dir.join("rust-encrypted.bin");
    fs::write(rust_file, encrypted).expect("Failed to write Rust encrypted file");
    
    // Clean up
    session.close().await.expect("Failed to close session");
}

#[tokio::test]
async fn test_encrypt_decrypt_compatibility() {
    // Create test components
    let kms = Arc::new(StaticKeyManagementService::new("static-master-key-for-testing".to_string()));
    let metastore = Arc::new(InMemoryMetastore::new());
    let persistence = Persistence::new(metastore, kms);
    let session_factory = SessionFactory::new(persistence, None);
    
    // Create partition with same identifiers used in other language tests
    let partition = Partition::new("service", "product");
    let session = session_factory.get_session(&partition).await.expect("Failed to create session");
    
    // Encrypt test data
    let plain_data = b"test-data-for-verification";
    let encrypted = session.encrypt(plain_data).await.expect("Failed to encrypt data");
    
    // Decrypt the data we just encrypted
    let decrypted = session.decrypt(&encrypted).await.expect("Failed to decrypt own data");
    
    // Verify round trip
    assert_eq!(&decrypted, plain_data, "Round-trip encryption/decryption failed");
    
    // Clean up
    session.close().await.expect("Failed to close session");
    
    println!("Round-trip encryption/decryption test passed!");
}

#[tokio::test]
async fn test_different_key_sizes() {
    // Test various key sizes to ensure compatibility
    let key_sizes = vec![16, 24, 32]; // 128, 192, and 256-bit keys
    
    for key_size in key_sizes {
        let master_key = "x".repeat(key_size);
        let kms = Arc::new(StaticKeyManagementService::new(master_key));
        let metastore = Arc::new(InMemoryMetastore::new());
        let persistence = Persistence::new(metastore, kms);
        let session_factory = SessionFactory::new(persistence, None);
        
        let partition = Partition::new("service", "product");
        let session = session_factory.get_session(&partition).await.expect("Failed to create session");
        
        // Encrypt and decrypt test data
        let plain_data = b"test-data";
        let encrypted = session.encrypt(plain_data).await.expect(&format!("Failed to encrypt data with {}-bit key", key_size * 8));
        let decrypted = session.decrypt(&encrypted).await.expect(&format!("Failed to decrypt data with {}-bit key", key_size * 8));
        
        // Verify decryption
        assert_eq!(&decrypted, plain_data, "Encryption/decryption with {}-bit key failed", key_size * 8);
        
        // Clean up
        session.close().await.expect("Failed to close session");
        
        println!("Key size {} bytes ({}-bit) test passed!", key_size, key_size * 8);
    }
}

/// Test that verifies the format of the encrypted data matches expectations
#[tokio::test]
async fn test_encrypted_data_format() {
    // Create test components
    let kms = Arc::new(StaticKeyManagementService::new("static-master-key-for-testing".to_string()));
    let metastore = Arc::new(InMemoryMetastore::new());
    let persistence = Persistence::new(metastore, kms);
    let session_factory = SessionFactory::new(persistence, None);
    
    // Create partition with same identifiers used in other language tests
    let partition = Partition::new("service", "product");
    let session = session_factory.get_session(&partition).await.expect("Failed to create session");
    
    // Encrypt test data
    let plain_data = b"test-data";
    let encrypted = session.encrypt(plain_data).await.expect("Failed to encrypt data");
    
    // Verify the encrypted data has the expected format
    // The minimum length should include:
    // - Key metadata (ID, created timestamp)
    // - Encrypted data key
    // - IV/nonce
    // - Encrypted data
    // - Authentication tag
    assert!(encrypted.len() > plain_data.len() + 32, "Encrypted data is too short");
    
    // Clean up
    session.close().await.expect("Failed to close session");
}

/// Instructions for running cross-language compatibility tests
#[allow(dead_code)]
fn instructions() -> &'static str {
    "
    Cross-Language Compatibility Testing
    ===================================
    
    To fully test cross-language compatibility:
    
    1. Create a 'test-data' directory in the parent directory
    
    2. Run each language implementation to generate encrypted test files:
       - Go: go test -v ./tests/cross_language_test.go
       - Java: mvn test -Dtest=CrossLanguageTest
       - C#: dotnet test --filter 'CrossLanguageTests'
       - Rust: cargo test --test compatibility
    
    3. Each implementation should:
       - Encrypt the test string 'test-data'
       - Save the encrypted data to a file named '{language}-encrypted.bin'
       - Read and decrypt files from other implementations
    
    4. Verify that all implementations can decrypt data encrypted by other implementations
    
    Note: All implementations must use the same static master key for this test to work.
    "
}