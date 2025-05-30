use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Testing DefaultSecretFactory...");

    // Create secret factory
    let factory = DefaultSecretFactory::new();
    println!("Secret factory created successfully");

    // Create a secret with data
    let mut data = b"This is a test secret".to_vec();
    let secret = factory.new(&mut data)?;
    println!("Secret created successfully with {} bytes", secret.len());

    // Verify the original data is wiped
    assert_ne!(data, b"This is a test secret");
    println!("Original data has been successfully wiped");

    // Read from the secret
    let reader_result = secret.reader();
    let mut buffer = vec![0u8; secret.len()];

    if let Ok(mut reader) = reader_result {
        reader.read_exact(&mut buffer)?;

        // Verify the data
        assert_eq!(&buffer, b"This is a test secret");
        println!("Successfully read data from secret and verified contents");
    } else {
        println!(
            "Failed to get reader from secret: {:?}",
            reader_result.err()
        );
        return Err("Failed to get reader".into());
    }

    // Use with_bytes to access the secret directly
    secret.with_bytes(|bytes| {
        assert_eq!(bytes, b"This is a test secret");
        println!("Successfully accessed secret data with with_bytes");
        Ok(())
    })?;

    // Create a second secret
    let mut data2 = b"This is a test secret".to_vec();
    let secret2 = factory.new(&mut data2)?;

    // Create a random secret
    let random_secret = factory.create_random(32)?;
    println!("Created random secret with {} bytes", random_secret.len());

    // Close the secrets
    secret.close()?;
    println!("Secret closed successfully");

    secret2.close()?;
    println!("Second secret closed successfully");

    random_secret.close()?;
    println!("Random secret closed successfully");

    println!("All secret operations completed successfully!");
    Ok(())
}
