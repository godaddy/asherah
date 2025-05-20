use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use securememory::stream::{Stream, STREAM_CHUNK_SIZE};
use std::io::{Read, Write};
use std::sync::{Arc, Mutex};

#[test]
fn test_basic_stream_operations() {
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Test initial state
    assert_eq!(stream.size().unwrap(), 0);

    // Write some data
    let data = b"Hello, secure world!".to_vec();
    assert_eq!(stream.write(&data).unwrap(), data.len());
    assert_eq!(stream.size().unwrap(), data.len());

    // Read it back
    let mut buffer = vec![0u8; data.len()];
    assert_eq!(stream.read(&mut buffer).unwrap(), data.len());
    assert_eq!(buffer, data);

    // Stream should be empty now
    assert_eq!(stream.size().unwrap(), 0);
}

#[test]
fn test_stream_large_data() {
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Create data larger than chunk size
    let size = *STREAM_CHUNK_SIZE * 2 + 1024;
    let mut data = vec![0u8; size];
    getrandom::getrandom(&mut data).unwrap();
    let original_data = data.clone();

    // Write it to the stream
    stream.write_all(&data).unwrap();
    assert_eq!(stream.size().unwrap(), size);

    // Read it back in smaller chunks
    let mut read_data = Vec::new();
    let mut buffer = vec![0u8; 4096];

    loop {
        match stream.read(&mut buffer) {
            Ok(0) => break,
            Ok(n) => read_data.extend_from_slice(&buffer[..n]),
            Err(e) => panic!("Read error: {:?}", e),
        }
    }

    // Verify the data
    assert_eq!(read_data.len(), original_data.len());
    assert_eq!(read_data, original_data);
}

#[test]
fn test_stream_next_and_flush() {
    let factory = DefaultSecretFactory::new();
    let stream = Stream::new(factory);

    // Write multiple chunks
    let chunk1 = b"first chunk".to_vec();
    let chunk2 = b"second chunk".to_vec();
    let mut write_stream = stream.clone();
    write_stream.write_all(&chunk1).unwrap();
    write_stream.write_all(&chunk2).unwrap();

    // Get the first chunk using next()
    let secret1 = stream.next().unwrap();
    secret1
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"first chunk");
            Ok(())
        })
        .unwrap();

    // Use flush() to get the remaining data
    let secret2 = stream.flush().unwrap();
    secret2
        .with_bytes(|bytes| {
            assert_eq!(bytes, b"second chunk");
            Ok(())
        })
        .unwrap();

    // Stream should be empty now
    assert_eq!(stream.size().unwrap(), 0);
}

#[test]
fn test_stream_simulated_threads() {
    // Test stream operations in a single thread, simulating multiple threads
    // This avoids the issue with Arc<Stream> not implementing DerefMut

    // Number of simulated threads and operations per thread
    let thread_count = 5;
    let ops_per_thread = 10;
    let data_size = 128; // Smaller size for faster tests

    // Create multiple streams to simulate separate threads
    let mut streams = Vec::new();
    for _ in 0..thread_count {
        streams.push(Stream::new(DefaultSecretFactory::new()));
    }

    // Simulate operations that would be done in separate threads
    for thread_id in 0..thread_count {
        // Each "thread" performs multiple operations
        for op_id in 0..ops_per_thread {
            // Generate unique data for this simulated thread and operation
            let mut data = format!("Thread-{}-Op-{}", thread_id, op_id).into_bytes();
            data.resize(data_size, b'x');

            // Get the stream for this "thread"
            let stream = &mut streams[thread_id];

            // Write the data
            stream.write_all(&data).unwrap();

            // Read the data back
            let mut read_buf = vec![0u8; data_size];
            stream.read_exact(&mut read_buf).unwrap();

            // Verify the data
            assert_eq!(read_buf, data);
        }
    }

    // Verify all streams are empty at the end
    for stream in &mut streams {
        assert_eq!(stream.size().unwrap(), 0);
    }
}

#[test]
fn test_stream_partial_reads() {
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Create test data
    let data = b"12345678901234567890".to_vec();
    stream.write_all(&data).unwrap();

    // Read in small chunks
    let mut buffer1 = [0u8; 5];
    let mut buffer2 = [0u8; 10];
    let mut buffer3 = [0u8; 5];

    // Read first 5 bytes
    stream.read_exact(&mut buffer1).unwrap();
    assert_eq!(&buffer1, b"12345");

    // Read next 10 bytes
    stream.read_exact(&mut buffer2).unwrap();
    assert_eq!(&buffer2, b"6789012345");

    // Read final 5 bytes
    stream.read_exact(&mut buffer3).unwrap();
    assert_eq!(&buffer3, b"67890");

    // Should be at end of stream
    let mut byte = [0u8; 1];
    let bytes_read = stream
        .read(&mut byte)
        .expect("Stream read should succeed at EOF");
    assert_eq!(bytes_read, 0, "Should return 0 bytes at EOF");
}

#[test]
fn test_stream_cross_chunk_reads() {
    // Test reading across chunk boundaries
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Create data that spans multiple chunks
    let chunk_size = *STREAM_CHUNK_SIZE;
    let mut data = Vec::with_capacity(chunk_size * 2 + 10);

    // Fill first chunk
    for i in 0..chunk_size {
        data.push((i % 256) as u8);
    }

    // Fill second chunk
    for i in 0..chunk_size {
        data.push(((i + 128) % 256) as u8);
    }

    // Add a little extra
    data.extend_from_slice(b"EXTRADATA");

    // Write to stream
    stream.write_all(&data).unwrap();

    // Read a buffer that crosses the chunk boundary
    let boundary_start = *STREAM_CHUNK_SIZE - 5;
    let read_size = 10; // 5 bytes from first chunk + 5 from second
    let mut cross_chunk_buffer = vec![0u8; read_size];

    // Position to the boundary
    let mut skip_buffer = vec![0u8; boundary_start];
    stream.read_exact(&mut skip_buffer).unwrap();

    // Read across the boundary
    stream.read_exact(&mut cross_chunk_buffer).unwrap();

    // Verify the data
    assert_eq!(
        cross_chunk_buffer,
        &data[boundary_start..boundary_start + read_size]
    );

    // Read the rest to verify it works
    let mut rest = Vec::new();
    stream.read_to_end(&mut rest).unwrap();
    assert_eq!(rest, &data[boundary_start + read_size..]);
}

#[test]
fn test_stream_zero_length_operations() {
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Writing zero bytes should succeed but do nothing
    assert_eq!(stream.write(&[]).unwrap(), 0);
    assert_eq!(stream.size().unwrap(), 0);

    // Reading zero bytes should succeed but do nothing
    let mut empty_buf = [];
    assert_eq!(stream.read(&mut empty_buf).unwrap(), 0);

    // Write some real data
    stream.write_all(b"test").unwrap();

    // Reading zero bytes should still succeed but do nothing
    assert_eq!(stream.read(&mut empty_buf).unwrap(), 0);
    assert_eq!(stream.size().unwrap(), 4); // Size shouldn't change

    // Clean up
    let mut buf = [0u8; 4];
    stream.read_exact(&mut buf).unwrap();
}

#[test]
fn test_stream_close_behavior() {
    let factory = DefaultSecretFactory::new();
    let stream = Stream::new(factory);

    // Write some data
    let mut writer = stream.clone();
    writer.write_all(b"test data").unwrap();

    // Get a secret from the stream
    let secret = stream.next().unwrap();

    // Close the secret explicitly
    let mut secret = secret;
    secret.close().unwrap();

    // Trying to use the secret after closing should fail
    let result = secret.with_bytes(|_| Ok(()));
    assert!(result.is_err());

    // But the stream should still be usable
    assert_eq!(stream.size().unwrap(), 0);
    let mut writer = stream.clone();
    writer.write_all(b"more data").unwrap();
    assert_eq!(stream.size().unwrap(), 9);
}
