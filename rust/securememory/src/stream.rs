use crate::error::{Result, SecureMemoryError};
use crate::secret::{Secret, SecretFactory, SecretExtensions};
use once_cell::sync::Lazy;
use std::collections::LinkedList;
use std::io::{self, Read, Write};
use std::sync::{Arc, Mutex};
use zeroize::Zeroize;

/// The default chunk size for Stream operations, set to 4 pages.
/// 
/// This determines the maximum size of each individual encrypted chunk
/// in the stream. If you encounter memory allocation errors, you might
/// need to increase your system's mlock limits. On Unix systems, use
/// 'ulimit -l' to check your current limits.
pub static STREAM_CHUNK_SIZE: Lazy<usize> = Lazy::new(|| page_size::get() * 4);

/// A queue for managing encrypted chunks of data.
///
/// This internal structure maintains a linked list of encrypted secrets,
/// providing methods to add data to the front or back of the queue and
/// retrieve data from the front.
struct Queue<S: Secret + SecretExtensions> {
    /// The linked list of secret chunks
    list: LinkedList<BoxedSecret<S>>,
}

impl<S: Secret + SecretExtensions> Queue<S> {
    /// Creates a new empty queue.
    fn new() -> Self {
        Self {
            list: LinkedList::new(),
        }
    }

    /// Adds data to the back of the queue.
    fn join(&mut self, secret: BoxedSecret<S>) {
        self.list.push_back(secret);
    }

    /// Adds data to the front of the queue.
    fn push(&mut self, secret: BoxedSecret<S>) {
        self.list.push_front(secret);
    }

    /// Pops data from the front of the queue.
    ///
    /// Returns None if the queue is empty.
    fn pop(&mut self) -> Option<BoxedSecret<S>> {
        self.list.pop_front()
    }

    /// Returns the total size of all secrets in the queue.
    fn size(&self) -> Result<usize> {
        let mut total = 0;
        for secret in &self.list {
            secret.with_bytes(|bytes| {
                total += bytes.len();
                Ok(())
            })?;
        }
        Ok(total)
    }

    /// Returns the number of chunks in the queue.
    #[allow(dead_code)]
    fn len(&self) -> usize {
        self.list.len()
    }

    /// Returns true if the queue is empty.
    fn is_empty(&self) -> bool {
        self.list.is_empty()
    }
}

/// An in-memory encrypted container implementing Read and Write traits.
///
/// Stream is a secure container for sensitive data that allows you to write
/// large amounts of data and read it back in chunks. Data is broken down into
/// fixed-size chunks and stored in protected memory, with each chunk being
/// individually protected when not in use.
///
/// This is most useful when you need to store lots of sensitive data in memory
/// and are able to work on it in chunks.
///
/// # Examples
///
/// ```rust,no_run
/// use securememory::protected_memory::DefaultSecretFactory;
/// use securememory::stream::Stream;
/// use std::io::{Read, Write};
///
/// // Create a new stream with a default secret factory
/// let factory = DefaultSecretFactory::new();
/// let mut stream = Stream::new(factory);
///
/// // Write sensitive data to the stream
/// let sensitive_data = b"This is sensitive information that should be protected in memory";
/// stream.write_all(sensitive_data).unwrap();
///
/// // Read data back from the stream
/// let mut buffer = [0u8; 16];
/// stream.read_exact(&mut buffer).unwrap();
/// assert_eq!(&buffer, b"This is sensitiv");
///
/// // Read more data
/// stream.read_exact(&mut buffer).unwrap();
/// assert_eq!(&buffer, b"e information th");
/// ```
/// A wrapper around a Box<dyn Secret> that also implements SecretExtensions
pub struct BoxedSecret<T: Secret + SecretExtensions> {
    inner: T
}

impl<T: Secret + SecretExtensions> BoxedSecret<T> {
    pub fn new(inner: T) -> Self {
        Self { inner }
    }
}

impl<T: Secret + SecretExtensions> Secret for BoxedSecret<T> {
    fn is_closed(&self) -> bool {
        self.inner.is_closed()
    }
    
    fn close(&self) -> Result<()> {
        self.inner.close()
    }
    
    fn reader(&self) -> Result<Box<dyn Read + Send + Sync + '_>> {
        self.inner.reader()
    }
    
    fn len(&self) -> usize {
        self.inner.len()
    }
}

impl<T: Secret + SecretExtensions> SecretExtensions for BoxedSecret<T> {
    fn with_bytes<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<R>,
    {
        self.inner.with_bytes(action)
    }
    
    fn with_bytes_func<F, R>(&self, action: F) -> Result<R>
    where
        F: FnOnce(&[u8]) -> Result<(R, Vec<u8>)>,
    {
        self.inner.with_bytes_func(action)
    }
}

pub struct Stream<F: SecretFactory> {
    /// The queue of encrypted chunks
    queue: Arc<Mutex<Queue<F::SecretType>>>,
    /// The factory used to create new secrets
    factory: F,
}

impl<F: SecretFactory + Clone> Clone for Stream<F> {
    fn clone(&self) -> Self {
        Self {
            queue: self.queue.clone(),
            factory: self.factory.clone(),
        }
    }
}

impl<F> Stream<F> 
where 
    F: SecretFactory + Clone + 'static,
    F::SecretType: Secret + SecretExtensions,
{
    /// Creates a new empty Stream with the given secret factory.
    ///
    /// # Arguments
    ///
    /// * `factory` - A secret factory implementation used to create encrypted chunks
    ///
    /// # Returns
    ///
    /// A new empty Stream instance.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use securememory::protected_memory::DefaultSecretFactory;
    /// use securememory::stream::Stream;
    ///
    /// let factory = DefaultSecretFactory::new();
    /// let stream = Stream::new(factory);
    /// ```
    pub fn new(factory: F) -> Self {
        Self {
            queue: Arc::new(Mutex::new(Queue::new())),
            factory,
        }
    }

    /// Returns the total number of bytes currently stored in the Stream.
    ///
    /// # Returns
    ///
    /// The total size in bytes of all data currently in the stream.
    ///
    /// # Errors
    ///
    /// Returns a `SecureMemoryError` if there is an issue accessing the
    /// protected memory.
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use securememory::protected_memory::DefaultSecretFactory;
    /// use securememory::stream::Stream;
    /// use std::io::Write;
    ///
    /// let factory = DefaultSecretFactory::new();
    /// let mut stream = Stream::new(factory);
    ///
    /// // Write some data
    /// stream.write_all(b"sensitive data").unwrap();
    ///
    /// // Check the size
    /// assert_eq!(stream.size().unwrap(), 14);
    /// ```
    pub fn size(&self) -> Result<usize> {
        let queue = self.queue.lock().map_err(|_| {
            SecureMemoryError::OperationFailed("Failed to acquire lock".to_string())
        })?;
        queue.size()
    }

    /// Returns the next chunk of data from the Stream.
    ///
    /// This method retrieves and removes the next available chunk of data from
    /// the stream, returning it as a boxed Secret.
    ///
    /// # Returns
    ///
    /// * `Ok(Box<dyn Secret>)` - The next chunk of data if available
    /// * `Err(SecureMemoryError)` - If there's no data or an error occurs
    ///
    /// # Errors
    ///
    /// * `SecureMemoryError::OperationFailed` - If the stream is empty or a lock error occurs
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use securememory::protected_memory::DefaultSecretFactory;
    /// use securememory::secret::{Secret, SecretExtensions};
    /// use securememory::stream::Stream;
    /// use std::io::Write;
    ///
    /// let factory = DefaultSecretFactory::new();
    /// let mut stream = Stream::new(factory);
    ///
    /// // Write some data
    /// stream.write_all(b"secret data").unwrap();
    ///
    /// // Get the next chunk as a Secret
    /// let secret = stream.next().unwrap();
    ///
    /// // Use the secret
    /// secret.with_bytes(|bytes| {
    ///     assert_eq!(bytes, b"secret data");
    ///     Ok(())
    /// }).unwrap();
    /// ```
    pub fn next(&self) -> Result<BoxedSecret<F::SecretType>> {
        let mut queue = self.queue.lock().map_err(|_| {
            SecureMemoryError::OperationFailed("Failed to acquire lock".to_string())
        })?;

        queue.pop().ok_or_else(|| {
            SecureMemoryError::OperationFailed("Stream is empty".to_string())
        })
    }

    /// Reads all data from the Stream and returns it as a single Secret.
    ///
    /// This method consumes all data in the stream, combining it into a
    /// single Secret.
    ///
    /// # Returns
    ///
    /// * `Ok(Box<dyn Secret>)` - A Secret containing all the stream's data
    /// * `Err(SecureMemoryError)` - If an error occurs during the operation
    ///
    /// # Errors
    ///
    /// * `SecureMemoryError::OperationFailed` - If the stream is empty or a lock error occurs
    /// * Other errors from the underlying SecretFactory or memory operations
    ///
    /// # Examples
    ///
    /// ```rust,no_run
    /// use securememory::protected_memory::DefaultSecretFactory;
    /// use securememory::secret::{Secret, SecretExtensions};
    /// use securememory::stream::Stream;
    /// use std::io::Write;
    ///
    /// let factory = DefaultSecretFactory::new();
    /// let mut stream = Stream::new(factory);
    ///
    /// // Write data in multiple chunks
    /// stream.write_all(b"first part").unwrap();
    /// stream.write_all(b" and second part").unwrap();
    ///
    /// // Flush all data into a single Secret
    /// let all_data = stream.flush().unwrap();
    ///
    /// // Verify the combined data
    /// all_data.with_bytes(|bytes| {
    ///     assert_eq!(bytes, b"first part and second part");
    ///     Ok(())
    /// }).unwrap();
    /// ```
    pub fn flush(&self) -> Result<F::SecretType> {
        // Get the total size first while holding the lock
        let size;
        {
            let queue = self.queue.lock().map_err(|_| {
                SecureMemoryError::OperationFailed("Failed to acquire lock".to_string())
            })?;
            
            size = queue.size()?;
        }
        
        if size == 0 {
            return Err(SecureMemoryError::OperationFailed(
                "Stream is empty".to_string(),
            ));
        }

        // Buffer to hold all the data
        let mut all_data = Vec::with_capacity(size);
        
        // Now drain the queue in a loop
        loop {
            // Scope the lock to ensure it's dropped before we call with_bytes
            let next_secret;
            {
                let mut queue = self.queue.lock().map_err(|_| {
                    SecureMemoryError::OperationFailed("Failed to acquire lock".to_string())
                })?;
                
                if queue.is_empty() {
                    break;
                }
                
                // Get the next chunk
                next_secret = queue.pop().unwrap();
            }
            
            // Process the secret outside the lock
            next_secret.with_bytes(|bytes| {
                all_data.extend_from_slice(bytes);
                Ok(())
            })?;
        }

        // Create a new secret with all the data
        let mut result = all_data;
        let secret = self.factory.new(&mut result)?;
        
        // Ensure we've wiped the buffer
        result.zeroize();
        
        Ok(secret)
    }
}

impl<F> Read for Stream<F> 
where 
    F: SecretFactory + Clone + 'static,
    F::SecretType: Secret + SecretExtensions,
{
    /// Reads data from the Stream into the provided buffer.
    ///
    /// This method reads from the next available chunk of data. If the
    /// buffer is smaller than the next chunk, the remaining data is
    /// re-encrypted and added back to the front of the queue.
    ///
    /// # Arguments
    ///
    /// * `buf` - The buffer to read into
    ///
    /// # Returns
    ///
    /// * `Ok(usize)` - The number of bytes read
    /// * `Err(io::Error)` - If an error occurs during reading
    ///
    /// # Notes
    ///
    /// If there is no data available, this method returns `Ok(0)` as per
    /// the Read trait contract, which indicates end of file.
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        if buf.is_empty() {
            return Ok(0);
        }

        // Check if there's any data available
        let next_secret;
        {
            let mut queue = self.queue.lock().map_err(|e| {
                io::Error::new(io::ErrorKind::Other, format!("Lock error: {}", e))
            })?;

            // Get the next chunk of data
            match queue.pop() {
                Some(secret) => {
                    next_secret = secret;
                },
                None => {
                    // Return 0 to indicate EOF as per Read trait contract
                    return Ok(0);
                }
            };
        }

        // Process the secret (outside the lock)
        let result = next_secret.with_bytes(|secret_bytes| {
            let bytes_to_read = std::cmp::min(buf.len(), secret_bytes.len());
            buf[..bytes_to_read].copy_from_slice(&secret_bytes[..bytes_to_read]);

            // If there's data left over, re-encrypt it and push to the front of the queue
            if bytes_to_read < secret_bytes.len() {
                let mut remaining_data = secret_bytes[bytes_to_read..].to_vec();
                
                match self.factory.new(&mut remaining_data) {
                    Ok(new_secret) => {
                        let mut queue = self.queue.lock().map_err(|e| {
                            SecureMemoryError::OperationFailed(format!("Lock error: {}", e))
                        })?;
                        queue.push(BoxedSecret::new(new_secret));
                        remaining_data.zeroize();
                        Ok(bytes_to_read)
                    },
                    Err(e) => Err(e),
                }
            } else {
                // All data was read
                Ok(bytes_to_read)
            }
        });

        match result {
            Ok(bytes_read) => Ok(bytes_read),
            Err(e) => Err(io::Error::new(io::ErrorKind::Other, e.to_string())),
        }
    }
}

impl<F> Write for Stream<F>
where 
    F: SecretFactory + Clone + 'static,
    F::SecretType: Secret + SecretExtensions,
{
    /// Writes data to the Stream, breaking it into chunks if necessary.
    ///
    /// This method breaks the data into chunks of size `STREAM_CHUNK_SIZE` and 
    /// adds each chunk to the stream as an encrypted Secret.
    ///
    /// # Arguments
    ///
    /// * `buf` - The data to write to the stream
    ///
    /// # Returns
    ///
    /// * `Ok(usize)` - The number of bytes written
    /// * `Err(io::Error)` - If an error occurs during writing
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        if buf.is_empty() {
            return Ok(0);
        }

        let chunk_size = *STREAM_CHUNK_SIZE;
        let mut queue = self.queue.lock().map_err(|e| {
            io::Error::new(io::ErrorKind::Other, format!("Lock error: {}", e))
        })?;

        let mut bytes_written = 0;
        for chunk_start in (0..buf.len()).step_by(chunk_size) {
            let chunk_end = std::cmp::min(chunk_start + chunk_size, buf.len());
            let mut chunk_data = buf[chunk_start..chunk_end].to_vec();
            
            match self.factory.new(&mut chunk_data) {
                Ok(secret) => {
                    queue.join(BoxedSecret::new(secret));
                    bytes_written += chunk_end - chunk_start;
                }
                Err(e) => {
                    // If we've already written some data, report partial success
                    if bytes_written > 0 {
                        return Ok(bytes_written);
                    }
                    return Err(io::Error::new(io::ErrorKind::Other, e.to_string()));
                }
            }
            
            // Zero the buffer that held our temporary chunk data
            chunk_data.zeroize();
        }

        Ok(bytes_written)
    }

    /// Flushes this output stream, ensuring all data is written.
    ///
    /// Since all writes are immediately added to the queue, this is a no-op.
    ///
    /// # Returns
    ///
    /// * `Ok(())` - Always returns success
    /// * `Err(io::Error)` - Never returns an error
    fn flush(&mut self) -> io::Result<()> {
        // All writes are immediately stored, so no flushing is needed
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protected_memory::DefaultSecretFactory;
    use std::io::{Read, Write};

    fn create_random_data(size: usize) -> Vec<u8> {
        let mut data = vec![0u8; size];
        getrandom::getrandom(&mut data).unwrap();
        data
    }

    fn write_to_stream<F>(stream: &mut Stream<F>, data: &[u8]) 
    where 
        F: SecretFactory + Clone + 'static,
        F::SecretType: Secret + SecretExtensions,
    {
        let result = stream.write_all(data);
        assert!(result.is_ok(), "Failed to write to stream: {:?}", result);
        
        // Ensure original data is still intact (not wiped)
        assert!(!data.iter().all(|&b| b == 0), "Original data was wiped");
    }

    fn read_from_stream<F>(stream: &mut Stream<F>, expected: &[u8]) 
    where 
        F: SecretFactory + Clone + 'static,
        F::SecretType: Secret + SecretExtensions,
    {
        let mut buffer = vec![0u8; expected.len()];
        let result = stream.read_exact(&mut buffer);
        assert!(result.is_ok(), "Failed to read from stream: {:?}", result);
        assert_eq!(buffer, expected, "Read data doesn't match expected data");
    }

    fn read_eof<F>(stream: &mut Stream<F>) 
    where 
        F: SecretFactory + Clone + 'static,
        F::SecretType: Secret + SecretExtensions,
    {
        let mut buffer = [0u8; 1];
        let bytes_read = stream.read(&mut buffer).expect("Read at EOF should succeed");
        assert_eq!(bytes_read, 0, "Should return 0 bytes at EOF");
    }

    #[test]
    fn test_stream_size() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);

        // Initially empty
        assert_eq!(stream.size().unwrap(), 0);

        // Write some data
        let data = b"test data".to_vec();
        stream.write_all(&data).unwrap();
        assert_eq!(stream.size().unwrap(), data.len());

        // Write more data
        let more_data = b" and more test data".to_vec();
        stream.write_all(&more_data).unwrap();
        assert_eq!(stream.size().unwrap(), data.len() + more_data.len());

        // Read some data
        let mut buffer = [0u8; 5];
        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(stream.size().unwrap(), data.len() + more_data.len() - 5);
    }

    #[test]
    fn test_stream_read_write() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);

        // Write and read small data
        let data = b"small test data".to_vec();
        write_to_stream(&mut stream, &data);
        read_from_stream(&mut stream, &data);
        read_eof(&mut stream);

        // Write and read data larger than chunk size
        let large_data = create_random_data(*STREAM_CHUNK_SIZE * 2 + 1024);
        let large_data_clone = large_data.clone();
        write_to_stream(&mut stream, &large_data);

        // Read back in chunks equal to chunk size
        for i in 0..2 {
            let start = i * *STREAM_CHUNK_SIZE;
            let end = start + *STREAM_CHUNK_SIZE;
            let expected = &large_data_clone[start..end];
            let mut buffer = vec![0u8; expected.len()];
            stream.read_exact(&mut buffer).unwrap();
            assert_eq!(buffer, expected);
        }

        // Read the remaining data
        let start = 2 * *STREAM_CHUNK_SIZE;
        let remaining = &large_data_clone[start..];
        let mut buffer = vec![0u8; remaining.len()];
        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(buffer, remaining);

        read_eof(&mut stream);
    }

    #[test]
    fn test_stream_partial_reads() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);

        // Write some data
        let data = create_random_data(100);
        let data_clone = data.clone();
        write_to_stream(&mut stream, &data);

        // Read in small chunks
        for i in 0..10 {
            let start = i * 10;
            let end = start + 10;
            let expected = &data_clone[start..end];
            let mut buffer = vec![0u8; expected.len()];
            stream.read_exact(&mut buffer).unwrap();
            assert_eq!(buffer, expected);
        }

        read_eof(&mut stream);
    }

    #[test]
    fn test_stream_next_flush() {
        let factory = DefaultSecretFactory::new();
        let stream = Stream::new(factory);

        // Create data larger than the chunk size
        let size = *STREAM_CHUNK_SIZE * 2 + 1024;
        let data = create_random_data(size);
        let data_clone = data.clone();

        // Write data to the stream
        let mut write_stream = stream.clone();
        write_stream.write_all(&data).unwrap();

        // Use next() to get the first chunk
        let secret = stream.next().unwrap();
        secret.with_bytes(|bytes| {
            assert_eq!(bytes.len(), *STREAM_CHUNK_SIZE);
            assert_eq!(bytes, &data_clone[..*STREAM_CHUNK_SIZE]);
            Ok(())
        }).unwrap();

        // Use flush() to get the remaining data
        let remaining = stream.flush().unwrap();
        remaining.with_bytes(|bytes| {
            assert_eq!(bytes.len(), size - *STREAM_CHUNK_SIZE);
            assert_eq!(bytes, &data_clone[*STREAM_CHUNK_SIZE..]);
            Ok(())
        }).unwrap();

        // Stream should be empty now
        assert_eq!(stream.size().unwrap(), 0);
    }

    #[test]
    fn test_stream_multiple_small_chunks() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);

        // Write multiple small chunks
        let chunk1 = b"first chunk".to_vec();
        let chunk2 = b"second chunk".to_vec();
        
        write_to_stream(&mut stream, &chunk1);
        write_to_stream(&mut stream, &chunk2);

        // Read back small pieces
        let mut buffer = [0u8; 4];
        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(&buffer, b"firs");

        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(&buffer, b"t ch");

        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(&buffer, b"unks");

        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(&buffer, b"econ");

        let mut buffer = [0u8; 7];
        stream.read_exact(&mut buffer).unwrap();
        assert_eq!(&buffer, b"d chunk");

        read_eof(&mut stream);
    }
    
    #[test]
    fn test_stream_flush_combined() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);

        // Write multiple chunks
        stream.write_all(b"part one").unwrap();
        stream.write_all(b" and part two").unwrap();

        // Get all data as a single secret
        let combined = stream.flush().unwrap();
        
        // Verify the combined data
        combined.with_bytes(|bytes| {
            assert_eq!(bytes, b"part one and part two");
            Ok(())
        }).unwrap();
        
        // Stream should be empty
        assert_eq!(stream.size().unwrap(), 0);
    }
    
    #[test]
    fn test_stream_empty_operations() {
        let factory = DefaultSecretFactory::new();
        let mut stream = Stream::new(factory);
        
        // Size should be 0
        assert_eq!(stream.size().unwrap(), 0);
        
        // Reading should return EOF
        read_eof(&mut stream);
        
        // Next should fail
        assert!(stream.next().is_err());
        
        // Flush should fail
        assert!(stream.flush().is_err());
        
        // Write empty should succeed but do nothing
        assert_eq!(stream.write(&[]).unwrap(), 0);
        assert_eq!(stream.size().unwrap(), 0);
    }
}