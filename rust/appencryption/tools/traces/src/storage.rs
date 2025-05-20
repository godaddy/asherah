use std::sync::Arc;
use tokio::sync::mpsc;
use std::io::{BufRead, BufReader};

use crate::{Provider, KeyType, FileReader};

/// Provider for storage traces
pub struct StorageProvider {
    reader: Arc<dyn FileReader>,
}

impl StorageProvider {
    /// Create a new storage provider
    pub fn new(reader: Arc<dyn FileReader>) -> Self {
        Self { reader }
    }
    
    /// Parse a line from the storage trace file to extract the block/LBA
    fn parse_line(&self, line: &str) -> Option<u64> {
        // Skip comment lines
        if line.starts_with('#') {
            return None;
        }
        
        // Split by whitespace and find the LBA (Logical Block Address)
        let fields: Vec<&str> = line.split_whitespace().collect();
        if fields.len() < 3 {
            return None;
        }
        
        // The LBA is typically the 3rd field in storage traces
        if let Ok(lba) = fields[2].parse::<u64>() {
            Some(lba)
        } else {
            None
        }
    }
}

impl Provider for StorageProvider {
    fn provide(&self, keys_tx: mpsc::Sender<Box<dyn KeyType>>) {
        let mut reader = self.reader.clone();
        
        tokio::spawn(async move {
            // Create a buffered reader that reads line by line
            let mut reader_wrapper = ReaderToRead::new(Arc::get_mut(&mut reader).unwrap());
            let mut buf_reader = BufReader::new(&mut reader_wrapper);
            let mut line = String::new();
            
            loop {
                line.clear();
                match buf_reader.read_line(&mut line) {
                    Ok(0) => break, // End of file
                    Ok(_) => {
                        // Parse the line to get the LBA
                        if let Some(lba) = StorageProvider::parse_line(&line) {
                            if keys_tx.send(Box::new(lba) as Box<dyn KeyType>).await.is_err() {
                                break;
                            }
                        }
                    },
                    Err(_) => break, // Error reading
                }
            }
            
            // Close the channel when done
            drop(keys_tx);
        });
    }
}

// Helper struct to adapt our FileReader to std::io::Read
struct ReaderToRead<'a> {
    reader: &'a mut dyn FileReader,
}

impl<'a> ReaderToRead<'a> {
    fn new(reader: &'a mut dyn FileReader) -> Self {
        Self { reader }
    }
}

impl<'a> std::io::Read for ReaderToRead<'a> {
    fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
        self.reader.read(buf)
    }
}