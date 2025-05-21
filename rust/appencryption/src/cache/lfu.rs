use std::collections::{BTreeMap, HashMap};
use std::fmt;
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

/// LFU (Least Frequently Used) cache implementation
pub struct LfuCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
    /// Current entries in the cache
    entries: RwLock<HashMap<K, Arc<V>>>,

    /// Frequency counter for each key
    frequencies: RwLock<HashMap<K, usize>>,

    /// Inverse frequency map for efficient eviction
    /// Maps frequency -> set of keys with that frequency
    frequency_list: RwLock<BTreeMap<usize, Vec<K>>>,

    /// Entry access timestamps
    access_times: RwLock<HashMap<K, Instant>>,

    /// Maximum number of entries in the cache
    capacity: usize,

    /// Optional callback when an item is evicted
    evict_callback: Option<EvictCallback<K, V>>,

    /// Optional time-to-live for entries
    ttl: Option<Duration>,
}

impl<K, V> fmt::Debug for LfuCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let entries_len = self
            .entries
            .read()
            .map(|entries| entries.len())
            .unwrap_or(0);

        f.debug_struct("LfuCache")
            .field("capacity", &self.capacity)
            .field("ttl", &self.ttl)
            .field("entries_len", &entries_len)
            .finish()
    }
}

impl<K, V> LfuCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    /// Create a new LFU cache with the given capacity
    pub fn new(
        capacity: usize,
        evict_callback: Option<EvictCallback<K, V>>,
        ttl: Option<Duration>,
    ) -> Self {
        Self {
            entries: RwLock::new(HashMap::with_capacity(capacity)),
            frequencies: RwLock::new(HashMap::with_capacity(capacity)),
            frequency_list: RwLock::new(BTreeMap::new()),
            access_times: RwLock::new(HashMap::with_capacity(capacity)),
            capacity,
            evict_callback,
            ttl,
        }
    }

    /// Check if an entry has expired
    fn is_expired(&self, key: &K) -> bool {
        if let Some(ttl) = self.ttl {
            if let Some(last_accessed) = self
                .access_times
                .read()
                .expect("Failed to acquire read lock for access_times")
                .get(key)
            {
                return last_accessed.elapsed() > ttl;
            }
        }
        false
    }

    /// Evict the least frequently used item from the cache
    fn evict_lfu(&self) -> bool {
        let mut frequency_list = self
            .frequency_list
            .write()
            .expect("Failed to acquire write lock for frequency_list");

        // Find the lowest frequency with at least one key
        if let Some((&freq, keys)) = frequency_list.iter_mut().next() {
            if let Some(key) = keys.pop() {
                // If this was the last key with this frequency, remove the frequency
                if keys.is_empty() {
                    frequency_list.remove(&freq);
                }

                // Remove the key from entries, frequencies, and access_times
                let mut entries = self
                    .entries
                    .write()
                    .expect("Failed to acquire write lock for entries");
                let mut frequencies = self
                    .frequencies
                    .write()
                    .expect("Failed to acquire write lock for frequencies");
                let mut access_times = self
                    .access_times
                    .write()
                    .expect("Failed to acquire write lock for access_times");

                if let Some(value) = entries.remove(&key) {
                    frequencies.remove(&key);
                    access_times.remove(&key);

                    // Call eviction callback if provided
                    if let Some(callback) = &self.evict_callback {
                        callback(&key, &value);
                    }

                    return true;
                }
            }
        }

        false
    }

    /// Update the frequency for a key
    fn update_frequency(&self, key: &K) {
        let mut frequencies = self
            .frequencies
            .write()
            .expect("Failed to acquire write lock for frequencies");
        let mut frequency_list = self
            .frequency_list
            .write()
            .expect("Failed to acquire write lock for frequency_list");
        let mut access_times = self
            .access_times
            .write()
            .expect("Failed to acquire write lock for access_times");

        // Update access time
        access_times.insert(key.clone(), Instant::now());

        // Get the current frequency
        let current_freq = frequencies.get(key).cloned().unwrap_or(0);

        // Remove key from current frequency list
        if current_freq > 0 {
            if let Some(keys) = frequency_list.get_mut(&current_freq) {
                if let Some(pos) = keys.iter().position(|k| k == key) {
                    keys.swap_remove(pos);
                }

                // If this was the last key with this frequency, remove the frequency
                if keys.is_empty() {
                    frequency_list.remove(&current_freq);
                }
            }
        }

        // Increment frequency
        let new_freq = current_freq + 1;
        frequencies.insert(key.clone(), new_freq);

        // Add key to new frequency list
        frequency_list
            .entry(new_freq)
            .or_default()
            .push(key.clone());
    }
}

impl<K, V> Cache<K, V> for LfuCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        // Check if key exists and hasn't expired
        if self.is_expired(key) {
            self.remove(key);
            return None;
        }

        let entries = self.entries.read().ok()?;
        if let Some(value) = entries.get(key) {
            // Update frequency
            self.update_frequency(key);
            return Some(value.clone());
        }

        None
    }

    fn insert(&self, key: K, value: V) -> bool {
        // If at capacity, evict the least frequently used item
        let current_len = {
            let entries = self
                .entries
                .read()
                .expect("Failed to acquire read lock for entries");
            entries.len()
        };

        if current_len >= self.capacity {
            self.evict_lfu();
        }

        // Insert new value
        let arc_value = Arc::new(value);
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        entries.insert(key.clone(), arc_value);

        // Initialize frequency to 1
        self.update_frequency(&key);

        true
    }

    fn remove(&self, key: &K) -> bool {
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        if let Some(value) = entries.remove(key) {
            // Update frequency data structures
            let mut frequencies = self
                .frequencies
                .write()
                .expect("Failed to acquire write lock for frequencies");
            let mut frequency_list = self
                .frequency_list
                .write()
                .expect("Failed to acquire write lock for frequency_list");
            let mut access_times = self
                .access_times
                .write()
                .expect("Failed to acquire write lock for access_times");

            if let Some(freq) = frequencies.remove(key) {
                if let Some(keys) = frequency_list.get_mut(&freq) {
                    if let Some(pos) = keys.iter().position(|k| k == key) {
                        keys.swap_remove(pos);
                    }

                    // If this was the last key with this frequency, remove the frequency
                    if keys.is_empty() {
                        frequency_list.remove(&freq);
                    }
                }
            }

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
        let entries = self
            .entries
            .read()
            .expect("Failed to acquire read lock for entries");
        entries.len()
    }

    fn capacity(&self) -> usize {
        self.capacity
    }

    fn clear(&self) {
        let mut entries = self
            .entries
            .write()
            .expect("Failed to acquire write lock for entries");
        let mut frequencies = self
            .frequencies
            .write()
            .expect("Failed to acquire write lock for frequencies");
        let mut frequency_list = self
            .frequency_list
            .write()
            .expect("Failed to acquire write lock for frequency_list");
        let mut access_times = self
            .access_times
            .write()
            .expect("Failed to acquire write lock for access_times");

        // Call eviction callback for all entries if provided
        if let Some(callback) = &self.evict_callback {
            for (key, value) in entries.iter() {
                callback(key, value);
            }
        }

        entries.clear();
        frequencies.clear();
        frequency_list.clear();
        access_times.clear();
    }

    fn close(&self) {
        self.clear();
    }
}
