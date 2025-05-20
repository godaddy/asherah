use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

fn main() {
    let factory = DefaultSecretFactory::new();
    let mut data = b"test data".to_vec();
    let secret = factory.new(&mut data).unwrap();
    
    // Get a reader
    let reader_result = secret.reader();
    let mut reader = reader_result.unwrap();
    
    // Read some data  
    let mut buffer = [0u8; 4];
    let read_result = reader.read(&mut buffer);
    println\!("Read before close result: {:?}", read_result);
    
    // Close the secret
    secret.close().unwrap(); 
    
    // Try to read again
    let read_result = reader.read(&mut buffer);
    println\!("Read after close result: {:?}", read_result);
}
EOF < /dev/null