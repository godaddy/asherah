use securememory::error::SecureMemoryError;
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};

#[test]
fn test_protected_memory_secret_creation() {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"testing".to_vec();

    // Create a secret
    let secret = factory.new(&mut orig).unwrap();

    // Verify it worked
    assert!(!secret.is_closed());

    // Access the data
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"testing");
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_secret_destruction() {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"testing".to_vec();

    // Create and explicitly destroy a secret
    let secret = factory.new(&mut orig).unwrap();
    secret.close().unwrap();

    // Verify it's closed
    assert!(secret.is_closed());

    // Trying to access it should fail
    let result = secret.with_bytes(|_| Ok(()));
    assert!(matches!(result, Err(SecureMemoryError::SecretClosed)));
}

#[test]
fn test_protected_memory_secret_drop() {
    let factory = DefaultSecretFactory::new();

    // Create a secret in a block so it's dropped at the end
    {
        let mut orig = b"testing".to_vec();
        let secret = factory.new(&mut orig).unwrap();

        // Verify it works
        secret
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"testing");
                Ok(())
            })
            .unwrap();

        // Secret will be dropped here
    }

    // Create another secret to ensure the factory still works after a drop
    let mut orig2 = b"testing2".to_vec();
    let secret2 = factory.new(&mut orig2).unwrap();

    secret2
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"testing2");
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_random_secret() {
    let factory = DefaultSecretFactory::new();
    let size = 32;

    // Create a random secret
    let secret = factory.create_random(size).unwrap();

    // Verify size and that it contains data
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes.len(), size);

            // Verify it's not all zeros (extremely unlikely with secure random)
            let all_zeros = bytes.iter().all(|&b| b == 0);
            assert!(!all_zeros, "Random secret shouldn't be all zeros");

            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_secret_with_zeros() {
    let factory = DefaultSecretFactory::new();
    let mut zeros = vec![0u8; 32];

    // Create a secret with all zeros
    let secret = factory.new(&mut zeros).unwrap();

    // Verify it worked
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes.len(), 32);
            assert!(bytes.iter().all(|&b| b == 0));
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_multiple_accesses() {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"testing_multiple_accesses".to_vec();

    let secret = factory.new(&mut orig).unwrap();

    // Access multiple times to ensure memory protection changes work properly
    for _ in 0..100 {
        secret
            .with_bytes(|bytes| {
                assert_eq!(bytes, b"testing_multiple_accesses");
                Ok(())
            })
            .unwrap();
    }
}

#[test]
fn test_protected_memory_with_bytes_func() {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"testing_with_bytes_func".to_vec();

    let secret = factory.new(&mut orig).unwrap();

    // Use with_bytes_func to return transformed data
    let result = secret
        .with_bytes_func(|bytes| {
            // Uppercase the data
            let uppercase = bytes
                .iter()
                .map(|&b| if b >= b'a' && b <= b'z' { b - 32 } else { b })
                .collect::<Vec<_>>();

            Ok(("transformed", uppercase))
        })
        .unwrap();

    assert_eq!(result, "transformed");

    // Verify original data is unchanged
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"testing_with_bytes_func");
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_reader() {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"testing_reader_functionality".to_vec();

    let secret = factory.new(&mut orig).unwrap();

    // Use the reader interface
    let reader_result = secret.reader();
    let mut buffer = Vec::new();

    match reader_result {
        Ok(mut reader) => {
            std::io::Read::read_to_end(&mut reader, &mut buffer).unwrap();
        }
        Err(e) => panic!("Failed to get reader: {}", e),
    }

    assert_eq!(buffer, b"testing_reader_functionality");
}

#[test]
fn test_protected_memory_null_bytes() {
    let factory = DefaultSecretFactory::new();
    let mut data = vec![0, 1, 2, 0, 3, 0, 4, 5, 0];

    let secret = factory.new(&mut data).unwrap();

    // Verify null bytes are preserved
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, &[0, 1, 2, 0, 3, 0, 4, 5, 0]);
            Ok(())
        })
        .unwrap();
}

#[test]
fn test_protected_memory_error_handling() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"error_test".to_vec();

    let secret = factory.new(&mut data).unwrap();

    // Test error propagation from the action closure
    let result: Result<(), SecureMemoryError> =
        secret.with_bytes(|_| Err(SecureMemoryError::OperationFailed("test error".to_string())));

    assert!(matches!(result, Err(SecureMemoryError::OperationFailed(_))));
    assert_eq!(
        result.unwrap_err().to_string(),
        "Secret operation failed: test error"
    );

    // Secret should still be usable after an error
    secret
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"error_test");
            Ok(())
        })
        .unwrap();
}
