use std::io::{BufRead, BufReader};
use std::sync::Arc;
use tokio::sync::mpsc;

use crate::{FileReader, KeyType, Provider};

/// Provider for YouTube traces
pub struct YouTubeProvider {
    reader: Arc<dyn FileReader>,
}

impl YouTubeProvider {
    /// Create a new YouTube provider
    pub fn new(reader: Arc<dyn FileReader>) -> Self {
        Self { reader }
    }

    /// Parse a line from the YouTube trace file to extract the video ID
    fn parse_line(&self, line: &str) -> Option<String> {
        // Skip comment lines
        if line.starts_with('#') {
            return None;
        }

        // Split by whitespace and find the video ID (usually the 2nd or 3rd field)
        let fields: Vec<&str> = line.split_whitespace().collect();
        if fields.len() < 3 {
            return None;
        }

        // The video ID is typically a field containing a pattern like "v=VIDEOID"
        for field in &fields {
            if field.contains("v=") {
                // Extract the video ID after "v="
                if let Some(pos) = field.find("v=") {
                    let video_id = &field[pos + 2..];
                    // Remove any trailing parameters
                    if let Some(param_pos) = video_id.find('&') {
                        return Some(video_id[..param_pos].to_string());
                    } else {
                        return Some(video_id.to_string());
                    }
                }
            }
        }

        // If we can't find a proper video ID, use a hash of the full line
        Some(format!(
            "hash-{}",
            line.bytes().fold(0u64, |acc, b| acc.wrapping_add(b as u64))
        ))
    }
}

impl Provider for YouTubeProvider {
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
                        // Parse the line to get the video ID
                        if let Some(video_id) = YouTubeProvider::parse_line(&line) {
                            if !video_id.is_empty() {
                                if keys_tx
                                    .send(Box::new(video_id) as Box<dyn KeyType>)
                                    .await
                                    .is_err()
                                {
                                    break;
                                }
                            }
                        }
                    }
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
