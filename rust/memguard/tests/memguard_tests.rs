use memguard::{Buffer, Enclave, MemguardError, scramble_bytes, wipe_bytes, purge};
use std::io::{Cursor, Read, Write}; // For reader tests
use serial_test::serial;

#[cfg(test)]
fn test_setup() {
    // Reset global state before each test
    memguard::reset_for_tests();
}

#[test]
fn test_buffer_basic_operations() {
    // Create a buffer
    let buffer = Buffer::new(64).expect("Buffer creation failed");
    
    // Write data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).expect("Writing to buffer failed");
    
    // Read data
    buffer.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).expect("Reading from buffer failed");
    
    // Destroy buffer
    let _ = buffer.destroy();
    
    // Verify destroyed
    assert!(!buffer.is_alive());
}

#[test]
fn test_buffer_scramble() {
    // Create a buffer
    let buffer = Buffer::new(64).expect("Buffer creation failed");
    
    // Fill with zeros
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = 0;
        }
        Ok(())
    }).expect("Writing to buffer failed");
    
    // Scramble the buffer
    buffer.scramble().expect("Scrambling buffer failed");
    
    // Verify the data has changed
    buffer.with_data(|data| {
        // Extremely unlikely that all bytes are still 0
        assert!(data.iter().any(|&b| b != 0));
        Ok(())
    }).expect("Reading from buffer failed");
}

#[test]
fn test_buffer_clone() {
    // Create a buffer
    let buffer = Buffer::new(64).expect("Buffer creation failed");
    
    // Write data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).expect("Writing to buffer failed");
    
    // Clone the buffer
    let buffer_clone = buffer.clone();
    
    // Verify the clone has the same data
    buffer_clone.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).expect("Reading from clone failed");
    
    // Destroying the original should not affect the clone
    let _ = buffer.destroy();
    assert!(!buffer.is_alive());
    assert!(buffer_clone.is_alive());
    
    // Clone should still have the data
    buffer_clone.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).expect("Reading from clone after original destroyed failed");
}

#[test]
#[serial] // Run this test serially to avoid coffer conflicts  
fn test_enclave_basic() {
    test_setup();
    
    // Create data
    let mut data = Vec::new();
    for i in 0..64 {
        data.push(i as u8);
    }
    
    let data_copy = data.clone();
    
    // Create an enclave
    let enclave = Enclave::new(&mut data).expect("Enclave creation failed");
    
    // Original data should be wiped
    assert_ne!(data, data_copy, "Original data was not wiped after enclave creation");
    
    // Open the enclave
    let opened_buffer = enclave.open().expect("Enclave opening failed");
    
    // Verify contents
    opened_buffer.with_data(|buffer_data| {
        assert_eq!(buffer_data.len(), 64);
        for i in 0..buffer_data.len() {
            assert_eq!(buffer_data[i], i as u8);
        }
        Ok(())
    }).expect("Reading from opened buffer failed");
}

#[test]
#[serial] // Run this test serially to avoid coffer conflicts
fn test_enclave_seal_open() {
    test_setup();
    
    // Create a buffer
    let mut buffer = Buffer::new(64).expect("Buffer creation failed");
    
    // Write data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).expect("Writing to buffer failed");
    
    // Seal the buffer into an enclave
    let enclave = Enclave::seal(&mut buffer).expect("Sealing buffer failed");
    
    // Buffer should be destroyed
    assert!(!buffer.is_alive(), "Buffer should be destroyed after sealing");
    
    // Verify enclave size
    assert_eq!(enclave.size(), 64, "Enclave size mismatch");
    
    // Open the enclave
    let unsealed = enclave.open().expect("Opening enclave failed");
    
    // Verify contents
    unsealed.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).expect("Reading from unsealed buffer failed");
}

#[test]
#[serial] // Run this test serially to avoid purge/coffer conflicts
fn test_multiple_enclaves() {
    test_setup();
    
    // Create multiple buffers with different data
    let mut buffers_orig = Vec::new();
    
    for j in 0..3 {
        let buffer = Buffer::new(32).expect("Buffer creation failed");
        
        // Write unique data pattern
        let current_j = j; // Capture j for the closure
        buffer.with_data_mut(move |data| {
            for i in 0..data.len() {
                data[i] = (i * current_j) as u8;
            }
            Ok(())
        }).expect("Writing to buffer failed");
        
        buffers_orig.push(buffer);
    }
    
    // Seal each buffer into an enclave
    let mut enclaves = Vec::new();
    for mut buffer_instance in buffers_orig { // Iterate and consume original buffers
        let enclave = Enclave::seal(&mut buffer_instance).expect("Sealing buffer failed");
        assert!(!buffer_instance.is_alive(), "Buffer should be destroyed after sealing");
        enclaves.push(enclave);
    }
    
    // Open each enclave and verify the contents
    for (j, enclave) in enclaves.iter().enumerate() {
        let unsealed = enclave.open().expect("Opening enclave failed");
        
        let current_j = j; // Capture j for the closure
        unsealed.with_data(move |data| {
            for i in 0..data.len() {
                assert_eq!(data[i], (i * current_j) as u8, "Data mismatch in unsealed enclave");
            }
            Ok(())
        }).expect("Reading from unsealed buffer failed");
    }
}

#[test]
fn test_public_scramble_bytes() {
    let mut buf = vec![0u8; 32];
    let original_buf = buf.clone();
    scramble_bytes(&mut buf);
    assert_ne!(buf, original_buf, "buffer not scrambled by public scramble_bytes");
    assert!(buf.iter().any(|&x| x != 0), "buffer seems to be all zeros after scramble");
}

#[test]
fn test_public_wipe_bytes() {
    let mut buf = vec![0u8; 32];
    // Fill with some data
    for i in 0..buf.len() {
        buf[i] = i as u8;
    }
    assert!(buf.iter().any(|&x| x != 0), "buffer should not be all zeros before wipe");
    wipe_bytes(&mut buf);
    assert!(buf.iter().all(|&x| x == 0), "buffer not wiped by public wipe_bytes");
}

#[test]
fn test_api_new_buffer() { // Corresponds to Go's TestNewBuffer
    let b = Buffer::new(32).expect("Buffer::new(32) failed");
    assert_eq!(b.size(), 32, "Buffer size incorrect");
    b.with_data(|data| {
        assert_eq!(data.len(), 32, "Inner data length incorrect");
        assert!(data.iter().all(|&x| x == 0), "Buffer not zeroed");
        Ok(())
    }).unwrap();
    // Rust Buffer::is_state_mutable() is a test-only helper.
    // For public API, we'd check if melt() then freeze() works, or try a mutable op.
    // Default state is mutable.
    assert!(b.is_alive(), "Buffer should be alive");
    b.destroy().unwrap();

    let b0 = Buffer::new(0).expect("Buffer::new(0) should return Ok(Buffer::null())");
    assert_eq!(b0.size(), 0, "Size of Buffer::new(0) should be 0");
    assert!(!b0.is_alive(), "Buffer::new(0) should be non-alive (null)");
    // No need to destroy b0 as it's a null buffer.
}

#[test]
fn test_api_new_buffer_from_bytes() { // Corresponds to Go's TestNewBufferFromBytes
    let mut original_data = b"yellow submarine".to_vec();
    let b = Buffer::new_from_bytes(&mut original_data).expect("NewBufferFromBytes failed");

    assert_eq!(b.size(), 16, "Buffer size from bytes incorrect");
    b.with_data(|data| {
        assert_eq!(data, b"yellow submarine");
        Ok(())
    }).unwrap();
    assert!(original_data.iter().all(|&x| x == 0), "Source data not wiped");
    // new_from_bytes makes it ReadOnly. Test this by trying to melt then write.
    // Or, if we had a public is_mutable, use that.
    // For now, assume it's ReadOnly as per implementation.
    // A melt followed by a successful write would confirm it was initially ReadOnly.
    b.melt().expect("Melt failed on buffer from bytes"); // Should succeed
    b.with_data_mut(|d| { d[0] = b'Y'; Ok(()) }).expect("Write after melt failed");
    b.freeze().expect("Freeze after melt failed"); // Revert to ReadOnly

    assert!(b.is_alive(), "Buffer from bytes should be alive");
    b.destroy().unwrap();

    let mut empty_data: Vec<u8> = vec![];
    let b0 = Buffer::new_from_bytes(&mut empty_data).expect("NewBufferFromBytes with empty data failed");
    assert_eq!(b0.size(), 0, "Size of Buffer from empty bytes should be 0");
    assert!(!b0.is_alive(), "Buffer from empty bytes should be non-alive (null)");
}

#[test]
fn test_api_new_buffer_from_reader() { // Corresponds to Go's TestNewBufferFromReader
    // Case 1: Successful full read
    let data1 = vec![7u8; 128];
    let mut reader1 = Cursor::new(data1.clone());
    let (b1, err1_opt) = Buffer::new_from_reader(&mut reader1, 128).expect("NewBufferFromReader case 1 failed");
    assert!(err1_opt.is_none(), "Case 1 should have no I/O error");
    assert_eq!(b1.size(), 128);
    b1.with_data(|d| { assert_eq!(d, data1.as_slice()); Ok(()) }).unwrap();
    // Check immutability (new_from_reader makes it ReadOnly)
    assert!(matches!(b1.with_data_mut(|_| Ok(())), Err(MemguardError::ProtectionFailed(_))), "Buffer from reader should be ReadOnly initially");
    b1.destroy().unwrap();

    // Case 2: Partial read (request more than available)
    let data2 = b"short".to_vec();
    let mut reader2 = Cursor::new(data2.clone());
    let (b2, err2_opt) = Buffer::new_from_reader(&mut reader2, 10).expect("NewBufferFromReader case 2 failed");
    assert!(err2_opt.is_some(), "Case 2 should have an I/O error (EOF)");
    if let Some(MemguardError::OperationFailed(msg)) = err2_opt { // Changed from IoError to OperationFailed as per new_from_reader impl
        assert!(msg.contains("EOF before filling buffer") || msg.contains("unexpected EOF"));
    } else {
        panic!("Expected OperationFailed EOF error for case 2, got {:?}", err2_opt);
    }
    assert_eq!(b2.size(), 5); // Should contain what was read
    b2.with_data(|d| { assert_eq!(d, data2.as_slice()); Ok(()) }).unwrap();
    b2.destroy().unwrap();

    // Case 3: Empty reader
    let mut reader3 = Cursor::new(Vec::<u8>::new());
    let (b3, err3_opt) = Buffer::new_from_reader(&mut reader3, 32).expect("NewBufferFromReader case 3 failed");
    assert!(err3_opt.is_some(), "Case 3 should have an I/O error (EOF)");
    assert_eq!(b3.size(), 0);
    assert!(!b3.is_alive()); // Null buffer

    // Case 4: Request 0 bytes
    let mut reader4 = Cursor::new(b"data".to_vec());
    let (b4, err4_opt) = Buffer::new_from_reader(&mut reader4, 0).expect("NewBufferFromReader case 4 failed");
    assert!(err4_opt.is_none(), "Case 4 should have no I/O error for 0 bytes");
    assert_eq!(b4.size(), 0);
    assert!(!b4.is_alive()); // Null buffer
}

#[test]
fn test_api_new_buffer_from_reader_until() { // Corresponds to Go's TestNewBufferFromReaderUntil
    // Case 1: Delimiter found
    let data1 = b"hello\nworld".to_vec();
    let mut reader1 = Cursor::new(data1.clone());
    let (b1, err1_opt) = Buffer::new_from_reader_until(&mut reader1, b'\n', None).expect("ReaderUntil case 1 failed");
    assert!(err1_opt.is_none());
    assert_eq!(b1.size(), 5);
    b1.with_data(|d| { assert_eq!(d, b"hello"); Ok(()) }).unwrap();
    b1.destroy().unwrap();
    // Check remaining in reader
    let mut remaining1 = Vec::new();
    reader1.read_to_end(&mut remaining1).unwrap();
    assert_eq!(remaining1, b"world");

    // Case 2: EOF before delimiter
    let data2 = b"short".to_vec();
    let mut reader2 = Cursor::new(data2.clone());
    let (b2, err2_opt) = Buffer::new_from_reader_until(&mut reader2, b'X', None).expect("ReaderUntil case 2 failed");
    assert!(err2_opt.is_some());
    if let Some(MemguardError::IoError(io_err)) = err2_opt {
        assert_eq!(io_err.kind(), std::io::ErrorKind::UnexpectedEof);
    } else {
        panic!("Expected IoError EOF for case 2, got {:?}", err2_opt);
    }
    assert_eq!(b2.size(), 5);
    b2.with_data(|d| { assert_eq!(d, b"short"); Ok(()) }).unwrap();
    b2.destroy().unwrap();

    // Case 3: Delimiter is first byte
    let mut reader3 = Cursor::new(b"Xrest".to_vec());
    let (b3, err3_opt) = Buffer::new_from_reader_until(&mut reader3, b'X', None).expect("ReaderUntil case 3 failed");
    assert!(err3_opt.is_none());
    assert_eq!(b3.size(), 0);
    assert!(!b3.is_alive()); // Null buffer

    // Case 4: Empty reader
    let mut reader4 = Cursor::new(Vec::<u8>::new());
    let (b4, err4_opt) = Buffer::new_from_reader_until(&mut reader4, b'X', None).expect("ReaderUntil case 4 failed");
    assert!(err4_opt.is_some()); // EOF
    assert_eq!(b4.size(), 0);
    assert!(!b4.is_alive());
}

#[test]
fn test_api_new_buffer_from_entire_reader() { // Corresponds to Go's TestNewBufferFromEntireReader
    // Case 1: Normal read
    let data1 = b"entire content".to_vec();
    let mut reader1 = Cursor::new(data1.clone());
    let (b1, err1_opt) = Buffer::new_from_entire_reader(&mut reader1).expect("EntireReader case 1 failed");
    assert!(err1_opt.is_none());
    assert_eq!(b1.size(), data1.len());
    b1.with_data(|d| { assert_eq!(d, data1.as_slice()); Ok(()) }).unwrap();
    b1.destroy().unwrap();

    // Case 2: Empty reader
    let mut reader2 = Cursor::new(Vec::<u8>::new());
    let (b2, err2_opt) = Buffer::new_from_entire_reader(&mut reader2).expect("EntireReader case 2 failed");
    assert!(err2_opt.is_none()); // EOF is not an error for "entire reader" if 0 bytes read
    assert_eq!(b2.size(), 0);
    assert!(!b2.is_alive()); // Null buffer

    // Case 3: Reader with an error after some data
    struct ErrorAfterData<'a> { data: &'a [u8], read_ptr: usize, error_on_read: usize }
    impl<'a> Read for ErrorAfterData<'a> {
        fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
            if self.read_ptr >= self.data.len() { return Ok(0); } // EOF
            if self.read_ptr >= self.error_on_read { return Err(std::io::Error::new(std::io::ErrorKind::Other, "simulated error")); }
            let max_bytes = std::cmp::min(self.error_on_read - self.read_ptr, self.data.len() - self.read_ptr);
            let bytes_to_read = std::cmp::min(buf.len(), max_bytes);
            buf[..bytes_to_read].copy_from_slice(&self.data[self.read_ptr..self.read_ptr + bytes_to_read]);
            self.read_ptr += bytes_to_read;
            Ok(bytes_to_read)
        }
    }
    let data3 = b"part1_error_part2".to_vec();
    let mut reader3 = ErrorAfterData { data: &data3, read_ptr: 0, error_on_read: 6 }; // Error after "part1_"
    let (b3, err3_opt) = Buffer::new_from_entire_reader(&mut reader3).expect("EntireReader case 3 failed");
    assert!(err3_opt.is_some());
    if let Some(MemguardError::IoError(io_err)) = err3_opt {
        assert_eq!(io_err.kind(), std::io::ErrorKind::Other);
        assert_eq!(io_err.to_string(), "simulated error");
    } else {
        panic!("Expected IoError for case 3, got {:?}", err3_opt);
    }
    assert_eq!(b3.size(), 6); // Should contain "part1_"
    b3.with_data(|d| { assert_eq!(d, b"part1_"); Ok(()) }).unwrap();
    b3.destroy().unwrap();
}

#[test]
fn test_api_new_buffer_random() { // Corresponds to Go's TestNewBufferRandom
    let b = Buffer::new_random(32).expect("NewBufferRandom failed");
    assert_eq!(b.size(), 32);
    let is_zeroed = b.with_data(|d| Ok(d.iter().all(|&x| x == 0))).unwrap();
    assert!(!is_zeroed, "Random buffer is all zeros");
    // Check immutability
    assert!(matches!(b.with_data_mut(|_| Ok(())), Err(MemguardError::ProtectionFailed(_))), "Random buffer should be ReadOnly initially");
    assert!(b.is_alive());
    b.destroy().unwrap();

    let b0 = Buffer::new_random(0).expect("NewBufferRandom(0) failed");
    assert_eq!(b0.size(), 0);
    assert!(!b0.is_alive()); // Null buffer
}

#[test]
fn test_api_buffer_freeze_melt() { // Corresponds to Go's TestFreeze and TestMelt
    let b = Buffer::new(8).expect("Buffer::new for freeze/melt failed");
    // Initial state: mutable
    b.with_data_mut(|d| { d[0] = 1; Ok(()) }).expect("Initial write failed, buffer not mutable?");

    b.freeze().expect("Freeze failed");
    // Should be immutable
    assert!(matches!(b.with_data_mut(|d| { d[0] = 2; Ok(()) }), Err(MemguardError::ProtectionFailed(_))), "Write to frozen buffer should fail or be hard");
    // Read should still work
    b.with_data(|d| { assert_eq!(d[0], 1); Ok(()) }).unwrap();

    b.freeze().expect("Idempotent freeze failed"); // Idempotency

    b.melt().expect("Melt failed");
    // Should be mutable again
    b.with_data_mut(|d| { d[0] = 3; Ok(()) }).expect("Write after melt failed");
    b.with_data(|d| { assert_eq!(d[0], 3); Ok(()) }).unwrap();

    b.melt().expect("Idempotent melt failed"); // Idempotency

    b.destroy().unwrap();
    assert!(matches!(b.freeze(), Err(MemguardError::SecretClosed)));
    assert!(matches!(b.melt(), Err(MemguardError::SecretClosed))); // Changed from expect to matches! for null buffer
}

// This test was previously here, ensuring it's correctly placed and not duplicated.
// If it was missing, this block would add it. If present, it ensures it's the same.
#[test]
fn test_api_new_buffer_from_entire_reader_with_file() {
    // Create a dummy file for testing
    let test_file_name = "memguard_test_file_entire_reader.tmp";
    let file_content = b"Content from a test file for NewBufferFromEntireReader.";
    {
        let mut file = std::fs::File::create(test_file_name).expect("Failed to create test file");
        file.write_all(file_content).expect("Failed to write to test file");
    }

    let mut file_reader = std::fs::File::open(test_file_name).expect("Failed to open test file for reading");
    let (b, err_opt) = Buffer::new_from_entire_reader(&mut file_reader).expect("NewBufferFromEntireReader with file failed");

    assert!(err_opt.is_none(), "Reading entire file should not produce I/O error option");
    assert_eq!(b.size(), file_content.len(), "Buffer size mismatch with file content");
    b.with_data(|d| { assert_eq!(d, file_content); Ok(()) }).unwrap();
    assert!(matches!(b.with_data_mut(|_| Ok(())), Err(MemguardError::ProtectionFailed(_))), "Buffer from entire reader should be ReadOnly");

    b.destroy().unwrap();
    std::fs::remove_file(test_file_name).expect("Failed to remove test file");
}

#[test]
fn test_api_buffer_seal() { // Corresponds to Go's TestSeal
    test_setup();
    let mut b = Buffer::new_random(32).expect("NewBufferRandom for seal test failed");
    let original_data = b.with_data(|d| Ok(d.to_vec())).unwrap();

    let enclave = Enclave::seal(&mut b).expect("Seal failed");
    assert!(!b.is_alive(), "Buffer should be destroyed after seal");

    let opened_b = enclave.open().expect("Open after seal failed");
    opened_b.with_data(|d| { assert_eq!(d, original_data.as_slice()); Ok(()) }).unwrap();
    opened_b.destroy().unwrap();

    // Seal a destroyed buffer
    let mut b_destroyed = Buffer::new(16).unwrap();
    b_destroyed.destroy().unwrap();
    assert!(matches!(Enclave::seal(&mut b_destroyed), Err(MemguardError::SecretClosed)));
}

#[test]
fn test_api_buffer_scramble_wipe() { // Corresponds to Go's TestScramble and TestWipe for LockedBuffer
    let b = Buffer::new(32).expect("Buffer::new for scramble/wipe failed");
    b.with_data_mut(|d| { d.fill(0xAA); Ok(()) }).unwrap(); // Fill with known pattern

    b.scramble().expect("Scramble failed");
    let scrambled_pattern_aa = b.with_data(|d| Ok(d.iter().all(|&x| x == 0xAA))).unwrap();
    assert!(!scrambled_pattern_aa, "Buffer content did not change after scramble");

    // Rust Buffer doesn't have a public `wipe()` method. Test via `with_data_mut`.
    b.with_data_mut(|d| { memguard::wipe_bytes(d); Ok(()) }).unwrap();
    let is_zeroed = b.with_data(|d| Ok(d.iter().all(|&x| x == 0))).unwrap();
    assert!(is_zeroed, "Buffer not wiped");

    b.destroy().unwrap();
    assert!(matches!(b.scramble(), Err(MemguardError::SecretClosed)));
    // No public wipe to test on destroyed, but with_data_mut would fail.

    // Create a new empty buffer to simulate a null buffer
    // Size 0 creates a null buffer which is immediately "destroyed"
    let b_null = Buffer::new(0).unwrap();
    assert!(matches!(b_null.scramble(), Err(MemguardError::SecretClosed)));
}

#[test]
fn test_api_buffer_size_destroy_alive_mutable() { // Combines Go's TestSize, TestDestroy, TestIsAlive, TestIsMutable
    let b = Buffer::new(123).expect("Buffer::new for size test failed");
    assert_eq!(b.size(), 123);
    assert!(b.is_alive());
    // is_state_mutable is test-only. Public API implies mutability by default.

    b.destroy().unwrap();
    assert_eq!(b.size(), 0, "Destroyed buffer size should be 0");
    assert!(!b.is_alive());
    // Check idempotency of destroy
    b.destroy().unwrap();
    assert_eq!(b.size(), 0);
    assert!(!b.is_alive());

    // Create a new empty buffer to simulate a null buffer
    let b_null = Buffer::new(0).unwrap();
    assert_eq!(b_null.size(), 0);
    assert!(!b_null.is_alive());
}

#[test]
fn test_api_buffer_copy_operations() { // Corresponds to Go's TestCopy and TestCopyAt
    let b = Buffer::new(16).expect("Buffer::new for copy failed");
    let src_data1 = b"yellow submarine"; // 16 bytes

    // Test Copy (offset 0)
    b.with_data_mut(|dest| {
        let len_to_copy = std::cmp::min(dest.len(), src_data1.len());
        dest[..len_to_copy].copy_from_slice(&src_data1[..len_to_copy]);
        Ok(())
    }).unwrap();
    b.with_data(|d| { assert_eq!(d, src_data1); Ok(()) }).unwrap();

    // Test CopyAt (offset)
    let src_data2 = b"marine"; // 6 bytes
    b.with_data_mut(|dest| { // b currently holds "yellow submarine"
        let offset = 7; // "yellow " <- place "marine" here
        assert!(offset + src_data2.len() <= dest.len(), "CopyAt would overflow");
        let len_to_copy = std::cmp::min(dest.len() - offset, src_data2.len());
        dest[offset..offset+len_to_copy].copy_from_slice(&src_data2[..len_to_copy]);
        Ok(())
    }).unwrap();
    // Expected: "yellow " + overwrite starting at offset 7 with "marine"
    // "yellow submarine" (original)
    //        ^marine    (write "marine" starting at position 7)
    // "yellow marineine" (result)
    b.with_data(|d| { assert_eq!(d, b"yellow marineine"); Ok(()) }).unwrap();

    b.destroy().unwrap();
    // Test on destroyed buffer
    assert!(matches!(b.with_data_mut(|_| Ok(())), Err(MemguardError::SecretClosed)));

    // Create a new empty buffer to simulate a null buffer
    let b_null = Buffer::new(0).unwrap();
    // Test on null buffer (should be no-op or error if trying to access state)
    // with_data_mut on null buffer will return SecretClosed.
    assert!(matches!(b_null.with_data_mut(|_| Ok(())), Err(MemguardError::SecretClosed)));
}

#[test]
fn test_api_buffer_move_operations() { // Corresponds to Go's TestMove and TestMoveAt
    // Test Move (offset 0) - similar to new_from_bytes
    let mut src_data1 = b"move this data".to_vec(); // 14 bytes
    let b1 = Buffer::new_from_bytes(&mut src_data1).unwrap(); // Wipes src_data1
    assert!(src_data1.iter().all(|&x| x == 0), "Source for b1 not wiped");
    b1.with_data(|d| { assert_eq!(d, b"move this data"); Ok(()) }).unwrap();
    b1.destroy().unwrap();

    // Test MoveAt (offset) - manual copy + wipe
    let b2 = Buffer::new(16).unwrap();
    let mut src_data2 = b"segment".to_vec(); // 7 bytes
    let original_src_data2_content = src_data2.clone();

    b2.with_data_mut(|dest| {
        let offset = 4;
        assert!(offset + src_data2.len() <= dest.len(), "MoveAt would overflow");
        let len_to_copy = std::cmp::min(dest.len() - offset, src_data2.len());
        dest[offset..offset+len_to_copy].copy_from_slice(&src_data2[..len_to_copy]);
        Ok(())
    }).unwrap();
    wipe_bytes(&mut src_data2); // Manual wipe for MoveAt concept

    assert!(src_data2.iter().all(|&x| x == 0), "Source for b2 (MoveAt) not wiped");
    b2.with_data(|d| {
        let mut expected = vec![0u8; 16];
        expected[4..4+original_src_data2_content.len()].copy_from_slice(&original_src_data2_content);
        assert_eq!(d, expected.as_slice());
        Ok(())
    }).unwrap();
    b2.destroy().unwrap();

    // Test on destroyed buffer
    let b_destroyed = Buffer::new(8).unwrap();
    b_destroyed.destroy().unwrap();
    let src_data3 = b"test".to_vec();
    assert!(matches!(b_destroyed.with_data_mut(|_| Ok(())), Err(MemguardError::SecretClosed)));
    // Source src_data3 should not be wiped if operation on buffer fails before wipe
    assert_eq!(src_data3, b"test".to_vec());
}

#[test]
#[serial] // Run this test serially to avoid coffer conflicts
fn test_api_new_enclave_random() { // Corresponds to Go's TestNewEnclaveRandom
    test_setup();
    
    let e = Enclave::new_random(32).expect("NewEnclaveRandom(32) failed");
    let b = e.open().expect("Open failed on random enclave");
    assert_eq!(b.size(), 32, "Opened buffer from random enclave has wrong size");
    let is_zeroed = b.with_data(|d| Ok(d.iter().all(|&x| x == 0))).unwrap();
    assert!(!is_zeroed, "Random enclave's content is all zeros");
    b.destroy().unwrap();

    match Enclave::new_random(0) {
        Err(MemguardError::OperationFailed(msg)) => {
            assert!(msg.contains("Enclave random size must be positive"));
        }
        _ => panic!("Expected OperationFailed for Enclave::new_random(0)"),
    }
}

#[test]
#[serial] // Run this test serially to avoid coffer conflicts  
fn test_api_enclave_open_properties() { // Covers parts of Go's TestOpen
    test_setup();
    let mut data = b"test data".to_vec();
    let e = Enclave::new(&mut data).expect("Enclave::new failed");
    let b = e.open().expect("Open failed");
    assert_eq!(e.size(), b.size(), "Enclave size and opened buffer size mismatch");
    // Check immutability of opened buffer (Go's Open returns frozen)
    assert!(matches!(b.with_data_mut(|_| Ok(())), Err(MemguardError::ProtectionFailed(_))), "Buffer opened from enclave should be ReadOnly");
    b.destroy().unwrap();
}

#[test]
fn test_api_buffer_equality_operations() { // Covers Go's TestEqualTo, TestCopy, TestMove concepts
    // Equality (conceptual, via with_data)
    let b1_data = b"content".to_vec();
    let mut b1_src = b1_data.clone();
    let b1 = Buffer::new_from_bytes(&mut b1_src).unwrap();

    let b1_equal = b1.with_data(|d| Ok(d == b1_data.as_slice())).unwrap();
    assert!(b1_equal);
    let b1_notequal = b1.with_data(|d| Ok(d == b"other")).unwrap();
    assert!(!b1_notequal);

    b1.destroy().unwrap();
    // Equality on destroyed buffer (with_data returns SecretClosed)
    assert!(matches!(b1.with_data(|_| Ok(false)), Err(MemguardError::SecretClosed)));

    // Copy concept (into buffer)
    let b_copy_dest = Buffer::new(16).unwrap();
    let copy_src_data = b"copy this";
    b_copy_dest.with_data_mut(|dest_slice| {
        let len = std::cmp::min(dest_slice.len(), copy_src_data.len());
        dest_slice[..len].copy_from_slice(&copy_src_data[..len]);
        Ok(())
    }).unwrap();
    b_copy_dest.with_data(|d| { assert_eq!(&d[..copy_src_data.len()], copy_src_data); Ok(()) }).unwrap();
    b_copy_dest.destroy().unwrap();

    // Move concept (into buffer, source wiped)
    // This is what Buffer::new_from_bytes does.
    let mut move_src_data = b"move this".to_vec();
    let b_move_dest = Buffer::new_from_bytes(&mut move_src_data).unwrap();
    assert!(move_src_data.iter().all(|&x| x == 0), "Move source not wiped");
    b_move_dest.with_data(|d| { assert_eq!(d, b"move this"); Ok(()) }).unwrap();
    b_move_dest.destroy().unwrap();
}

// TestPtrSafetyWithGC from Go is not applicable to Rust's memory model.

#[test]
#[serial] // Run this test serially since it calls purge()
fn test_purge_logic() {
    // Simple test to verify purge behavior
    
    // 1. Create a buffer
    let buffer1 = Buffer::new(64).expect("Failed to create buffer");
    println!("TEST: Buffer1 created at address: {:p}", &buffer1);
    assert!(buffer1.is_alive(), "Buffer1 should be alive initially");

    // Check if buffer is actually destroyed
    let destroyed_before = buffer1.destroyed();
    println!("TEST: buffer1.destroyed() before purge = {}", destroyed_before);

    // 2. Call purge()
    println!("TEST: About to call purge()");
    purge();
    println!("TEST: purge() returned");

    // 3. Verify buffer is destroyed
    println!("TEST: Checking if buffer1 is alive");
    let destroyed_after = buffer1.destroyed();
    println!("TEST: buffer1.destroyed() after purge = {}", destroyed_after);
    let is_alive = buffer1.is_alive();
    println!("TEST: buffer1.is_alive() = {}", is_alive);
    assert!(!is_alive, "Buffer1 should be destroyed after purge");
}

#[test]
fn test_new_buffer_from_reader() {
    use std::io::Cursor; // Already covered by use std::io::{Cursor, Read, Write};
    use std::io::Read; // Already covered
    use std::io::ErrorKind; // Already covered by use std::io::{Cursor, Read, Write};
    // use memguard::Buffer; // Already covered by use memguard::{Buffer, ...};
    // use memguard::MemguardError; // Already covered

    // Case 1: Successful read of exact size
    let data1 = vec![1u8; 64];
    let mut reader1 = Cursor::new(data1.clone());
    let (buffer1, err1) = Buffer::new_from_reader(&mut reader1, 64).unwrap();
    assert!(err1.is_none());
    assert_eq!(buffer1.size(), 64);
    assert!(buffer1.is_alive());
    buffer1
        .with_data(|d| {
            assert_eq!(d, data1.as_slice());
            Ok(())
        })
        .unwrap();
    // Check immutability (protect sets mutable=false if ReadOnly)
    // Buffer::new_from_reader makes the buffer ReadOnly.
    // Attempting a mutable operation should fail.
    assert!(matches!(buffer1.with_data_mut(|_| Ok(())), Err(MemguardError::ProtectionFailed(_))));
    buffer1.destroy().unwrap();

    // Case 2: Read "yellow submarine" (16 bytes) requesting 16 bytes
    let data2 = b"yellow submarine".to_vec();
    let mut reader2 = Cursor::new(data2.clone());
    let (buffer2, err2) = Buffer::new_from_reader(&mut reader2, 16).unwrap();
    assert!(err2.is_none());
    assert_eq!(buffer2.size(), 16);
    buffer2
        .with_data(|d| {
            assert_eq!(d, data2.as_slice());
            Ok(())
        })
        .unwrap();
    buffer2.destroy().unwrap();

    // Case 3: Read "yellow submarine" (16 bytes) requesting 17 bytes (partial read due to EOF)
    let data3 = b"yellow submarine".to_vec();
    let mut reader3 = Cursor::new(data3.clone());
    let (buffer3, err3) = Buffer::new_from_reader(&mut reader3, 17).unwrap();
    assert!(err3.is_some()); // Expect an I/O error (OperationFailed wrapping UnexpectedEof)
    if let Some(MemguardError::OperationFailed(msg)) = err3 {
        assert!(msg.contains("EOF before filling buffer") || msg.contains("I/O error during read"));
    } else {
        panic!("Expected OperationFailed with EOF message, got {:?}", err3);
    }
    assert_eq!(buffer3.size(), 16); // Buffer should contain what was read
    buffer3
        .with_data(|d| {
            assert_eq!(d, data3.as_slice());
            Ok(())
        })
        .unwrap();
    buffer3.destroy().unwrap();

    // Case 4: Read from empty reader, request 32 bytes
    let data4: Vec<u8> = Vec::new();
    let mut reader4 = Cursor::new(data4);
    let (buffer4, err4) = Buffer::new_from_reader(&mut reader4, 32).unwrap();
    assert!(err4.is_some()); // Expect an I/O error
    assert_eq!(buffer4.size(), 0); // Should be a null buffer
    assert!(!buffer4.is_alive()); // Null buffer is not alive
                                 // No need to destroy, null buffer's destroy is a no-op.

    // Case 5: Request 0 bytes
    let data5 = vec![1, 2, 3];
    let mut reader5 = Cursor::new(data5);
    let (buffer5, err5) = Buffer::new_from_reader(&mut reader5, 0).unwrap();
    assert!(err5.is_none());
    assert_eq!(buffer5.size(), 0);
    assert!(!buffer5.is_alive());

    // Case 6: Reader returns an error other than EOF
    struct ErrorReader;
    impl Read for ErrorReader {
        fn read(&mut self, _buf: &mut [u8]) -> std::io::Result<usize> {
            Err(std::io::Error::new(ErrorKind::Other, "test error"))
        }
    }
    let mut reader6 = ErrorReader;
    let (buffer6, err6) = Buffer::new_from_reader(&mut reader6, 32).unwrap();
    assert!(err6.is_some());
    if let Some(MemguardError::OperationFailed(msg)) = err6 {
        assert!(msg.contains("test error"));
    } else {
        panic!("Expected OperationFailed with 'test error', got {:?}", err6);
    }
    assert_eq!(buffer6.size(), 0); // If error on first read, 0 bytes read, null buffer
    assert!(!buffer6.is_alive());
}
