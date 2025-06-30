use securememory::memguard::Enclave;

// This is a simple test for the Enclave type
// It only tests Enclave::new which doesn't depend on so many global components
#[test]
fn test_enclave_new() {
    // Create test data
    let mut test_data = vec![0u8; 64];
    for i in 0..test_data.len() {
        test_data[i] = (i % 256) as u8;
    }

    // Make a copy to verify wiping
    let original_data = test_data.clone();

    // Create an enclave directly
    let enclave = Enclave::new(&mut test_data).unwrap();

    // Verify original data was wiped
    assert_ne!(test_data, original_data);

    // Verify enclave reports correct size
    assert_eq!(enclave.size(), 64);
}
