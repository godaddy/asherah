use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Test basic secret functionality
    println!("Creating DefaultSecretFactory...");
    let factory = DefaultSecretFactory::new();
    println!("Secret factory created successfully!");

    // Test secret creation
    println!("Testing secret creation...");
    let mut data = vec![1, 2, 3, 4, 5]; // Some test data
    let original_data = data.clone();

    println!("Creating secret...");
    let secret = factory.new(&mut data)?;
    println!("Secret created successfully, size: {}", secret.len());

    // Verify that original data was wiped
    println!("Original data after wipe: {:?}", data);
    assert_ne!(data, original_data);

    // Access the secret
    println!("Accessing secret with read-only access...");
    secret.with_bytes(|bytes| {
        println!("Successfully accessed {} bytes: {:?}", bytes.len(), bytes);
        assert_eq!(bytes, &original_data);
        Ok(())
    })?;

    // Test cloning
    println!("Testing secret cloning...");
    let secret_clone = secret.clone();

    secret_clone.with_bytes(|bytes| {
        println!("Clone accessed {} bytes: {:?}", bytes.len(), bytes);
        assert_eq!(bytes, &original_data);
        Ok(())
    })?;

    // Close the secret
    println!("Closing secret...");
    secret.close()?;
    println!("Secret closed successfully");

    // Verify that we can't access after closing
    match secret.with_bytes(|_| Ok(())) {
        Err(e) => println!("Expected error accessing closed secret: {}", e),
        Ok(_) => panic!("Should not be able to access closed secret"),
    }

    Ok(())
}
