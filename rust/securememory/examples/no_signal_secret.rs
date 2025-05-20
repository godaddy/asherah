use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

// This example explicitly does not use signals to avoid potential issues

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Creating factory without signal handling...");
    let factory = DefaultSecretFactory::new();
    println!("Secret factory created successfully");

    // Create a small secret to minimize potential issues
    let mut data = b"test".to_vec();
    println!("Original data: {:?}", data);

    println!("Creating secret...");
    let secret = factory.new(&mut data)?;
    println!("Secret created, length: {}", secret.len());

    // Check that original data was wiped
    println!("Data after wiping: {:?}", data);

    // Read back the data using with_bytes
    secret.with_bytes(|bytes| {
        println!("Read data with with_bytes: {:?}", bytes);
        Ok(())
    })?;

    // Close the secret
    secret.close()?;
    println!("Secret closed successfully");

    println!("Test completed successfully");
    Ok(())
}
