use securememory::memguard::Buffer;

#[test]
fn test_buffer_basic() {
    // Fix the coffer and registry issues by only using a local buffer
    let buffer = Buffer::new(32).unwrap();

    // Write some test data
    buffer
        .with_data_mut(|data| {
            for i in 0..data.len() {
                data[i] = (i % 256) as u8;
            }
            Ok(())
        })
        .unwrap();

    // Read it back
    buffer
        .with_data(|data| {
            // Check a few values
            assert_eq!(data[0], 0);
            assert_eq!(data[1], 1);
            assert_eq!(data[5], 5);
            Ok(())
        })
        .unwrap();

    // Free the buffer
    buffer.destroy().unwrap();
}
