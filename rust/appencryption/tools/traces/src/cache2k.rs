use byteorder::{LittleEndian, ReadBytesExt};
use std::io::{Cursor, Read};
use std::sync::Arc;
use tokio::sync::mpsc;

use crate::{FileReader, KeyType, Provider};

/// Provider for Cache2k traces
pub struct Cache2kProvider {
    reader: Arc<dyn FileReader>,
}

impl Cache2kProvider {
    /// Create a new Cache2k provider
    pub fn new(reader: Arc<dyn FileReader>) -> Self {
        Self { reader }
    }
}

impl Provider for Cache2kProvider {
    fn provide(&self, keys_tx: mpsc::Sender<Box<dyn KeyType>>) {
        let mut reader = self.reader.clone();

        tokio::spawn(async move {
            let mut buffer = [0u8; 4];

            loop {
                match Arc::get_mut(&mut reader).unwrap().read(&mut buffer) {
                    Ok(0) => break, // End of file
                    Ok(4) => {
                        let mut cursor = Cursor::new(&buffer);
                        match cursor.read_u32::<LittleEndian>() {
                            Ok(key) => {
                                if keys_tx
                                    .send(Box::new(key) as Box<dyn KeyType>)
                                    .await
                                    .is_err()
                                {
                                    break;
                                }
                            }
                            Err(_) => break,
                        }
                    }
                    Ok(_) => break,  // Partial read, unexpected
                    Err(_) => break, // Error reading
                }
            }

            // Close the channel when done
            drop(keys_tx);
        });
    }
}
