use std::sync::Arc;
use rand::prelude::*;
use rand_distr::ZipfDistribution;
use tokio::sync::mpsc;

use crate::{Provider, KeyType};

/// Provider that generates keys from a Zipf distribution
pub struct ZipfProvider {
    /// Zipf distribution exponent (s)
    s: f64,
    /// Number of items to generate
    num: usize,
    /// Number of unique items
    n: u64,
}

impl ZipfProvider {
    /// Create a new Zipf provider
    /// 
    /// # Arguments
    /// * `s` - Zipf distribution exponent (s > 1.0)
    /// * `num` - Number of items to generate
    /// * `n` - Number of unique items (defaults to 2^16-1)
    pub fn new(s: f64, num: usize, n: Option<u64>) -> Self {
        if s <= 1.0 || num == 0 {
            panic!("Invalid Zipf parameters: s must be > 1.0 and num must be > 0");
        }
        
        Self {
            s,
            num,
            n: n.unwrap_or(u16::MAX as u64),
        }
    }
}

impl Provider for ZipfProvider {
    fn provide(&self, keys_tx: mpsc::Sender<Box<dyn KeyType>>) {
        let s = self.s;
        let num = self.num;
        let n = self.n;
        
        tokio::spawn(async move {
            // Create a deterministic random number generator
            let mut rng = StdRng::seed_from_u64(1);
            
            // Create a Zipf distribution
            let zipf = match ZipfDistribution::new(n, s) {
                Ok(z) => z,
                Err(_) => {
                    eprintln!("Failed to create Zipf distribution");
                    return;
                }
            };
            
            // Generate and send keys
            for _ in 0..num {
                let value = rng.sample(zipf);
                
                if keys_tx.send(Box::new(value) as Box<dyn KeyType>).await.is_err() {
                    break;
                }
            }
            
            // Close the channel when done
            drop(keys_tx);
        });
    }
}