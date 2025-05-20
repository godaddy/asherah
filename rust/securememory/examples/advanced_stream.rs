use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::SecretExtensions;
use securememory::stream::Stream;
use std::io::{Read, Write};
use std::thread;
use std::time::Duration;

/// This example demonstrates more advanced usage of the Stream API for multi-threaded
/// processing of sensitive data. It shows how to:
///
/// 1. Share a stream across threads for parallel processing
/// 2. Process large amounts of sensitive data with minimal memory footprint
/// 3. Implement a producer-consumer pattern for secure data processing
/// 4. Handle stream backpressure and timeouts
/// 5. Properly close and clean up resources

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("Advanced Stream Example");
    println!("======================");

    // Create a stream with a default secret factory
    let factory = DefaultSecretFactory::new();
    let mut stream = Stream::new(factory);

    // Clone the stream for the consumer thread
    let consumer_stream = stream.clone();

    // Create a channel to signal when the producer is done
    let (tx, rx) = std::sync::mpsc::channel();

    // Launch the consumer thread first
    let consumer = thread::spawn(move || {
        let mut consumer_stream = consumer_stream;

        // Buffer for processing chunks of data
        let mut buffer = vec![0u8; 64];
        let mut total_processed = 0;

        // Process until we receive the done signal
        loop {
            // Check if we should stop
            if rx.try_recv().is_ok() {
                println!(
                    "Consumer: Received stop signal. Total processed: {} bytes",
                    total_processed
                );
                break;
            }

            // Try to read some data
            match consumer_stream.read(&mut buffer) {
                Ok(bytes_read) if bytes_read > 0 => {
                    // Process the data (in a real application, you would do something useful here)
                    // Here we just print the first few bytes for demonstration
                    let display_bytes = std::cmp::min(bytes_read, 8);
                    let data_preview = &buffer[..display_bytes];
                    let preview = data_preview
                        .iter()
                        .map(|b| format!("{:02x}", b))
                        .collect::<Vec<String>>()
                        .join(" ");

                    println!(
                        "Consumer: Processing {} bytes. Preview: [{}...]",
                        bytes_read, preview
                    );

                    total_processed += bytes_read;

                    // Simulate some processing time
                    thread::sleep(Duration::from_millis(10));
                }
                Ok(_) => {
                    // No data available yet, wait a bit before trying again
                    thread::sleep(Duration::from_millis(10));
                }
                Err(e) => {
                    if e.kind() == std::io::ErrorKind::UnexpectedEof {
                        // No data available, wait a bit before trying again
                        thread::sleep(Duration::from_millis(10));
                    } else {
                        // Real error
                        eprintln!("Consumer: Error reading from stream: {}", e);
                        break;
                    }
                }
            }
        }

        // Final cleanup - in a real application, you might want to process any remaining data
        match consumer_stream.flush() {
            Ok(final_secret) => {
                final_secret
                    .with_bytes(|bytes| {
                        if !bytes.is_empty() {
                            println!(
                                "Consumer: Processed final {} bytes from the stream",
                                bytes.len()
                            );
                            total_processed += bytes.len();
                        }
                        Ok(())
                    })
                    .unwrap();
            }
            Err(e) => {
                if e.to_string() != "Stream is empty" {
                    eprintln!("Consumer: Error flushing stream: {}", e);
                }
            }
        }

        println!(
            "Consumer: Thread completed. Total processed: {} bytes",
            total_processed
        );
        total_processed
    });

    // Producer thread - generate and write sensitive data to the stream
    let producer = thread::spawn(move || {
        // Generate several chunks of random data to simulate sensitive information
        for i in 0..10 {
            // Generate random data (simulating sensitive information)
            let chunk_size = 128 * (i + 1); // Increase size with each chunk
            let mut sensitive_data = vec![0u8; chunk_size];
            getrandom::getrandom(&mut sensitive_data).unwrap();

            println!(
                "Producer: Writing chunk {} with {} bytes",
                i + 1,
                chunk_size
            );

            // Write data to the stream
            match stream.write_all(&sensitive_data) {
                Ok(_) => {
                    println!("Producer: Successfully wrote chunk {}", i + 1);
                }
                Err(e) => {
                    eprintln!("Producer: Error writing to stream: {}", e);
                    break;
                }
            }

            // Simulate data arriving over time
            thread::sleep(Duration::from_millis(50));
        }

        println!("Producer: All data written. Sending stop signal to consumer.");

        // Signal that we're done producing data
        tx.send(()).unwrap();
    });

    // Wait for both threads to complete
    producer.join().unwrap();
    let bytes_processed = consumer.join().unwrap();

    println!(
        "Main: Both threads completed. Total bytes processed: {}",
        bytes_processed
    );
    println!("Main: Stream memory has been securely wiped.");

    Ok(())
}
