// A safe test for the actual enclave module (using the successful patterns from custom_buffer_test)

use securememory::memguard::Enclave;

#[test]
fn test_enclave_direct() {
    // Create test data instead of using a Buffer
    let mut test_data = vec![0u8; 64];

    // Fill with pattern
    for i in 0..test_data.len() {
        test_data[i] = i as u8;
    }

    // Make a copy to verify wiping
    let original_data = test_data.clone();

    // Create enclave directly with the data
    println!("Creating enclave...");
    let enclave = Enclave::new(&mut test_data).unwrap();
    println!("Enclave created successfully");

    // Verify original data was wiped
    assert_ne!(test_data, original_data);

    // Verify enclave size
    assert_eq!(enclave.size(), 64);
}
