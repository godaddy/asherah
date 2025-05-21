use std::collections::{BTreeMap, HashMap};
use std::hash::{Hash, Hasher};
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

// Size of the TinyLFU sketch (number of counters)
const SKETCH_SIZE: usize = 4096;
// Decay factor for frequency counters
const DECAY_FACTOR: f64 = 0.5;
// Number of accesses between each decay
const DECAY_INTERVAL: usize = 10000;

/// TinyLFU (Tiny Least Frequently Used) cache implementation
///
/// This implementation uses a count-min sketch to approximate frequency counting
/// and an admission policy to improve cache hit rates.
pub struct TlfuCache<K, V> {
    /// Current entries in the cache
    entries: RwLock<HashMap<K, Arc<V>>>,

    /// Frequency counter for each key (using count-min sketch)
    sketch: RwLock<Vec<u16>>,

    /// Recency queue (for admission policy)
    window: RwLock<BTreeMap<Instant, K>>,

    /// Entry access timestamps
    access_times: RwLock<HashMap<K, Instant>>,

    /// Maximum number of entries in the cache
    capacity: usize,

    /// Number of accesses since last decay
    access_count: RwLock<usize>,

    /// Optional callback when an item is evicted
    evict_callback: Option<EvictCallback<K, V>>,

    /// Optional time-to-live for entries
    ttl: Option<Duration>,
}

impl<K, V> TlfuCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    /// Create a new TinyLFU cache with the given capacity
    pub fn new(
        capacity: usize,
        evict_callback: Option<EvictCallback<K, V>>,
        ttl: Option<Duration>,
    ) -> Self {
        Self {
            entries: RwLock::new(HashMap::with_capacity(capacity)),
            sketch: RwLock::new(vec![0; SKETCH_SIZE]),
            window: RwLock::new(BTreeMap::new()),
            access_times: RwLock::new(HashMap::with_capacity(capacity)),
            capacity,
            access_count: RwLock::new(0),
            evict_callback,
            ttl,
        }
    }

    /// Check if an entry has expired
    fn is_expired(&self, key: &K) -> bool {
        if let Some(ttl) = self.ttl {
            if let Some(last_accessed) = self.access_times.read().unwrap().get(key) {
                return last_accessed.elapsed() > ttl;
            }
        }
        false
    }

    /// Compute hash for a key
    fn hash_key(&self, key: &K) -> [usize; 4] {
        let mut hasher = std::collections::hash_map::DefaultHasher::new();
        Hash::hash(key, &mut hasher);
        let h = hasher.finish();

        // Use 4 different hash functions derived from h
        [
            (h & 0xFFFF) as usize % SKETCH_SIZE,
            ((h >> 16) & 0xFFFF) as usize % SKETCH_SIZE,
            ((h >> 32) & 0xFFFF) as usize % SKETCH_SIZE,
            ((h >> 48) & 0xFFFF) as usize % SKETCH_SIZE,
        ]
    }

    /// Increment the frequency count for a key
    fn increment_frequency(&self, key: &K) {
        let mut sketch = self.sketch.write().unwrap();
        let hashes = self.hash_key(key);

        for &h in &hashes {
            sketch[h] = sketch[h].saturating_add(1);
        }

        // Update access count and possibly decay counters
        let mut access_count = self.access_count.write().unwrap();
        *access_count += 1;

        if *access_count >= DECAY_INTERVAL {
            // Reset access count
            *access_count = 0;

            // Decay all counters
            for counter in sketch.iter_mut() {
                *counter = (*counter as f64 * DECAY_FACTOR) as u16;
            }
        }
    }

    /// Get the estimated frequency count for a key
    fn get_frequency(&self, key: &K) -> u16 {
        let sketch = self.sketch.read().unwrap();
        let hashes = self.hash_key(key);

        // Return the minimum count from all hash functions
        hashes.iter().map(|&h| sketch[h]).min().unwrap_or(0)
    }

    /// Evict an item from the cache using the Tiny-LFU policy
    fn evict_tlfu(&self) -> bool {
        // Try to admit an item from the window if available
        let mut window = self.window.write().unwrap();
        if !window.is_empty() {
            // Get most recent candidate from window (drop the borrow immediately)
            let (time, window_key) = {
                let last_entry = window.iter().next_back().unwrap();
                (*last_entry.0, last_entry.1.clone())
            };
            let window_key = window_key.clone();

            // If main cache is not full, admit without eviction
            let entries_len = {
                let entries = self.entries.read().unwrap();
                entries.len()
            };

            if entries_len < self.capacity {
                // Remove from window
                window.remove(&time);

                // Add to entries (happened outside this function when calling insert)
                return true;
            }

            // Find victim in main cache (lowest frequency)
            let mut min_freq = u16::MAX;
            let mut victim_key = None;

            {
                let entries = self.entries.read().unwrap();
                for key in entries.keys() {
                    let freq = self.get_frequency(key);
                    if freq < min_freq {
                        min_freq = freq;
                        victim_key = Some(key.clone());
                    }
                }
            }

            if let Some(victim_key) = victim_key {
                // Compare frequency of victim with window candidate
                let window_freq = self.get_frequency(&window_key);

                if window_freq > min_freq {
                    // Remove from window
                    window.remove(&time);

                    // Evict victim
                    self.remove(&victim_key);

                    // Key from window will be added in the insert method
                    return true;
                }
            }
        }

        // If no admission from window, evict the lowest frequency item
        let mut min_freq = u16::MAX;
        let mut victim_key = None;

        {
            let entries = self.entries.read().unwrap();
            for key in entries.keys() {
                let freq = self.get_frequency(key);
                if freq < min_freq {
                    min_freq = freq;
                    victim_key = Some(key.clone());
                }
            }
        }

        if let Some(victim_key) = victim_key {
            self.remove(&victim_key);
            return true;
        }

        false
    }
}

impl<K, V> Cache<K, V> for TlfuCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        // Check if key exists and hasn't expired
        if self.is_expired(key) {
            self.remove(key);
            return None;
        }

        // Check if key is in main cache
        let entries = self.entries.read().unwrap();
        if let Some(value) = entries.get(key) {
            // Update frequency and access time
            self.increment_frequency(key);

            let mut access_times = self.access_times.write().unwrap();
            access_times.insert(key.clone(), Instant::now());

            return Some(value.clone());
        }

        None
    }

    fn insert(&self, key: K, value: V) -> bool {
        let entries_len = {
            let entries = self.entries.read().unwrap();
            entries.len()
        };

        // If cache is full, use TinyLFU admission policy
        if entries_len >= self.capacity {
            // If new item frequency is high enough, it will replace a lower frequency item
            self.evict_tlfu();
        }

        // Insert new value into main cache
        let arc_value = Arc::new(value);
        let mut entries = self.entries.write().unwrap();

        // Skip if key already exists
        if entries.contains_key(&key) {
            return false;
        }

        entries.insert(key.clone(), arc_value);

        // Initialize frequency and access time
        self.increment_frequency(&key);

        let mut access_times = self.access_times.write().unwrap();
        access_times.insert(key, Instant::now());

        true
    }

    fn remove(&self, key: &K) -> bool {
        let mut entries = self.entries.write().unwrap();
        if let Some(value) = entries.remove(key) {
            // Update access times
            let mut access_times = self.access_times.write().unwrap();
            access_times.remove(key);

            // Call eviction callback if provided
            if let Some(callback) = &self.evict_callback {
                callback(key, &value);
            }

            return true;
        }

        false
    }

    fn len(&self) -> usize {
        let entries = self.entries.read().unwrap();
        entries.len()
    }

    fn capacity(&self) -> usize {
        self.capacity
    }

    fn clear(&self) {
        let mut entries = self.entries.write().unwrap();
        let mut window = self.window.write().unwrap();
        let mut access_times = self.access_times.write().unwrap();
        let mut sketch = self.sketch.write().unwrap();

        // Call eviction callback for all entries if provided
        if let Some(callback) = &self.evict_callback {
            for (key, value) in entries.iter() {
                callback(key, value);
            }
        }

        entries.clear();
        window.clear();
        access_times.clear();

        // Reset sketch counters
        for counter in sketch.iter_mut() {
            *counter = 0;
        }
    }

    fn close(&self) {
        self.clear();
    }
}
