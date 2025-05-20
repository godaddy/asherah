use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Test the protected memory secret with factory
    println!("Creating DefaultSecretFactory...");
    let factory = DefaultSecretFactory::new();
    println!("Factory created successfully!");
    
    // Create test data
    println!("Creating test data...");
    let mut test_data = b"This is a test secret with some data in it. It should be protected.".to_vec();
    
    // Create a secret
    println!("Creating secret...");
    let secret = factory.new(&mut test_data)?;
    println!("Secret created successfully!");
    
    // Test with_bytes
    println!("Testing with_bytes...");
    let expected_data = b"This is a test secret with some data in it. It should be protected.";
    let result = secret.with_bytes(|bytes| {
        println!("Inside with_bytes, secret has {} bytes", bytes.len());
        assert_eq!(bytes, expected_data);
        Ok(bytes.len())
    })?;
    println!("with_bytes successful, returned: {}", result);
    
    // Test reader
    println!("Testing reader...");
    let mut reader = secret.reader()?;
    let mut buf = Vec::new();
    let bytes_read = std::io::Read::read_to_end(&mut reader, &mut buf)?;
    println!("Reader read {} bytes", bytes_read);
    assert_eq!(buf, expected_data);
    
    // Test close
    println!("Closing secret...");
    secret.close()?;
    println!("Secret closed successfully!");
    
    // Test closed state
    println!("Verifying closed state...");
    assert!(secret.is_closed());
    
    // Test error handling with closed secret
    println!("Testing reader on closed secret...");
    assert!(secret.reader().is_err());
    
    println!("All tests passed successfully!");
    Ok(())
}