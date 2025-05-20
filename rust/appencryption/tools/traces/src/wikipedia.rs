use std::sync::Arc;
use tokio::sync::mpsc;
use std::io::{BufRead, BufReader};

use crate::{Provider, KeyType, FileReader};

/// Provider for Wikipedia traces
pub struct WikipediaProvider {
    reader: Arc<dyn FileReader>,
}

impl WikipediaProvider {
    /// Create a new Wikipedia provider
    pub fn new(reader: Arc<dyn FileReader>) -> Self {
        Self { reader }
    }
    
    /// Parse a line from the Wikipedia trace file to extract the URL path
    fn parse_line(&self, line: &[u8]) -> Option<String> {
        // Find "http://" in the line
        let http_index = line.windows(7).position(|window| window == b"http://")?;
        let line = &line[http_index + 7..];
        
        // Find the first slash after the domain
        let domain_end = line.iter().position(|&b| b == b'/')?;
        let path = &line[domain_end..];
        
        // Skip parameters (anything after ? or space)
        let param_end = path.iter()
            .position(|&b| b == b'?' || b == b' ')
            .unwrap_or(path.len());
        
        let path = &path[..param_end];
        
        // Convert to string
        String::from_utf8(path.to_vec()).ok()
    }
}

impl Provider for WikipediaProvider {
    fn provide(&self, keys_tx: mpsc::Sender<Box<dyn KeyType>>) {
        let mut reader = self.reader.clone();
        
        tokio::spawn(async move {
            // Create a buffered reader that reads line by line
            let mut reader_wrapper = ReaderToRead::new(Arc::get_mut(&mut reader).unwrap());
            let mut buf_reader = BufReader::new(&mut reader_wrapper);
            let mut line = Vec::new();
            
            loop {
                line.clear();
                match buf_reader.read_until(b'\n', &mut line) {
                    Ok(0) => break, // End of file
                    Ok(_) => {
                        // Parse the line to get the URL path
                        if let Some(url_path) = WikipediaProvider::parse_line(&line) {
                            if !url_path.is_empty() {
                                if keys_tx.send(Box::new(url_path) as Box<dyn KeyType>).await.is_err() {
                                    break;
                                }
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