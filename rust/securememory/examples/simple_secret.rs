// Simple test for the DefaultSecretFactory
// This example only uses the most minimal set of functionality in the SecretFactory
// to test if it works without signal handling, Stream, or other advanced features
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("\nBasic secret management test");

    // Create a secret factory
    let factory = DefaultSecretFactory::new();
    println!("Secret factory created");

    // Create a secret from data
    let mut data = b"Hello, secure world!".to_vec();
    let secret = factory.new(&mut data)?;
    println!("Secret created with {} bytes", secret.len());

    // The original data has been wiped
    assert_ne!(data, b"Hello, secure world!");
    println!("Original data successfully wiped");

    // Access the secret data
    secret.with_bytes(|bytes| {
        println!("Secret data accessed, first byte: {}", bytes[0]);
        assert_eq!(bytes, b"Hello, secure world!");
        Ok(())
    })?;

    // Create a random secret
    let random_secret = factory.create_random(32)?;
    println!("Random secret created with {} bytes", random_secret.len());

    // Access the random secret
    random_secret.with_bytes(|bytes| {
        println!("Random secret first byte: {}", bytes[0]);
        assert_eq!(bytes.len(), 32);
        Ok(())
    })?;

    // Use with_bytes_func to return a value
    let result = secret.with_bytes_func(|bytes| {
        let first_byte = bytes[0];
        Ok((first_byte, vec![first_byte]))
    })?;
    println!("First byte from with_bytes_func: {}", result);

    // Close the secrets
    secret.close()?;
    random_secret.close()?;

    println!("Secrets successfully closed");

    Ok(())
}
