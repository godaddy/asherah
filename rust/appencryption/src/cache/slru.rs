use std::collections::{HashMap, VecDeque};
use std::fmt;
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

/// Protected segment ratio for SLRU
const PROTECTED_RATIO: f64 = 0.8;

/// Entry in the SLRU cache with metadata
#[derive(Debug)]
struct SlruEntry<V>
where
    V: fmt::Debug,
{
    value: Arc<V>,
    last_accessed: Instant,
    protected: bool,
}

/// Segmented LRU cache implementation
///
/// SLRU divides the cache into two segments:
/// - Protected segment: frequently accessed items
/// - Probation segment: recently added items
///
/// Items are first admitted to the probation segment. When accessed again,
/// they are promoted to the protected segment. This prevents cache pollution
/// from one-time accesses.
pub struct SlruCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
    /// Current entries in the cache
    entries: RwLock<HashMap<K, SlruEntry<V>>>,

    /// Protected segment queue
    protected_queue: RwLock<VecDeque<K>>,

    /// Probation segment queue
    probation_queue: RwLock<VecDeque<K>>,

    /// Maximum total capacity
    capacity: usize,

    /// Maximum size of protected segment
    protected_capacity: usize,

    /// Optional callback when an item is evicted
    evict_callback: Option<EvictCallback<K, V>>,

    /// Optional time-to-live for entries
    ttl: Option<Duration>,
}

impl<K, V> fmt::Debug for SlruCache<K, V>
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

        f.debug_struct("SlruCache")
            .field("capacity", &self.capacity)
            .field("protected_capacity", &self.protected_capacity)
            .field("ttl", &self.ttl)
            .field("entries_len", &entries_len)
            .finish()
    }
}

impl<K, V> SlruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    /// Create a new SLRU cache with the given capacity
    pub fn new(
        capacity: usize,
        evict_callback: Option<EvictCallback<K, V>>,
        ttl: Option<Duration>,
    ) -> Self {
        let protected_capacity = (capacity as f64 * PROTECTED_RATIO) as usize;
        let probation_capacity = capacity - protected_capacity;

        Self {
            entries: RwLock::new(HashMap::with_capacity(capacity)),
            protected_queue: RwLock::new(VecDeque::with_capacity(protected_capacity)),
            probation_queue: RwLock::new(VecDeque::with_capacity(probation_capacity)),
            capacity,
            protected_capacity,
            evict_callback,
            ttl,
        }
    }

    /// Check if an entry has expired
    fn is_expired(&self, entry: &SlruEntry<V>) -> bool {
        if let Some(ttl) = self.ttl {
            return entry.last_accessed.elapsed() > ttl;
        }
        false
    }

    /// Evict an item from the cache
    fn evict(&self) -> bool {
        let Ok(mut entries) = self.entries.write() else {
            return false;
        };
        let Ok(mut probation_queue) = self.probation_queue.write() else {
            return false;
        };
        let Ok(mut protected_queue) = self.protected_queue.write() else {
            return false;
        };

        // First try to evict from probation
        while let Some(key) = probation_queue.pop_back() {
            if let Some(entry) = entries.remove(&key) {
                // Call eviction callback if provided
                if let Some(callback) = &self.evict_callback {
                    callback(&key, &entry.value);
                }
                return true;
            }
        }

        // If probation is empty, evict from protected
        while let Some(key) = protected_queue.pop_back() {
            if let Some(entry) = entries.remove(&key) {
                // Call eviction callback if provided
                if let Some(callback) = &self.evict_callback {
                    callback(&key, &entry.value);
                }
                return true;
            }
        }

        false
    }

    /// Promote an item from probation to protected segment
    fn promote_to_protected(&self, key: &K) {
        let Ok(mut probation_queue) = self.probation_queue.write() else {
            return;
        };
        let Ok(mut protected_queue) = self.protected_queue.write() else {
            return;
        };
        let Ok(mut entries) = self.entries.write() else {
            return;
        };

        // Remove from probation queue
        if let Some(pos) = probation_queue.iter().position(|k| k == key) {
            probation_queue.remove(pos);
        }

        // Update entry status
        if let Some(entry) = entries.get_mut(key) {
            entry.protected = true;
            entry.last_accessed = Instant::now();
        }

        // Add to protected queue
        protected_queue.push_front(key.clone());

        // If protected segment is too large, demote the least recently used item
        if protected_queue.len() > self.protected_capacity {
            if let Some(demote_key) = protected_queue.pop_back() {
                if let Some(entry) = entries.get_mut(&demote_key) {
                    entry.protected = false;
                }
                probation_queue.push_front(demote_key);
            }
        }
    }

    /// Update the LRU status for a key
    fn update_lru(&self, key: &K) {
        let Ok(mut entries) = self.entries.write() else {
            return;
        };
        let entry = match entries.get_mut(key) {
            Some(e) => e,
            None => return,
        };

        entry.last_accessed = Instant::now();
        let protected = entry.protected;
        drop(entries);

        if protected {
            // Already in protected segment, just update position
            let Ok(mut protected_queue) = self.protected_queue.write() else {
                return;
            };
            if let Some(pos) = protected_queue.iter().position(|k| k == key) {
                protected_queue.remove(pos);
            }
            protected_queue.push_front(key.clone());
        } else {
            // Promote from probation to protected
            self.promote_to_protected(key);
        }
    }
}

impl<K, V> Cache<K, V> for SlruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        let entries = self.entries.read().ok()?;

        if let Some(entry) = entries.get(key) {
            if self.is_expired(entry) {
                drop(entries);
                self.remove(key);
                return None;
            }

            let value = entry.value.clone();
            drop(entries);

            self.update_lru(key);
            return Some(value);
        }

        None
    }

    fn insert(&self, key: K, value: V) -> bool {
        let Ok(mut entries) = self.entries.write() else {
            return false;
        };

        // Check if we need to evict
        if entries.len() >= self.capacity && !entries.contains_key(&key) {
            drop(entries);
            self.evict();
            let Ok(new_entries) = self.entries.write() else {
                return false;
            };
            entries = new_entries;
        }

        // Update existing entry
        if let Some(entry) = entries.get_mut(&key) {
            entry.value = Arc::new(value);
            entry.last_accessed = Instant::now();
            let protected = entry.protected;
            drop(entries);

            if protected {
                if let Ok(mut protected_queue) = self.protected_queue.write() {
                    if let Some(pos) = protected_queue.iter().position(|k| k == &key) {
                        protected_queue.remove(pos);
                    }
                    protected_queue.push_front(key);
                }
            } else {
                self.promote_to_protected(&key);
            }

            return true;
        }

        // Insert new entry into probation
        let entry = SlruEntry {
            value: Arc::new(value),
            last_accessed: Instant::now(),
            protected: false,
        };

        entries.insert(key.clone(), entry);
        drop(entries);

        if let Ok(mut probation_queue) = self.probation_queue.write() {
            probation_queue.push_front(key);
        }

        true
    }

    fn remove(&self, key: &K) -> bool {
        let Ok(mut entries) = self.entries.write() else {
            return false;
        };

        if let Some(entry) = entries.remove(key) {
            if entry.protected {
                if let Ok(mut protected_queue) = self.protected_queue.write() {
                    if let Some(pos) = protected_queue.iter().position(|k| k == key) {
                        protected_queue.remove(pos);
                    }
                }
            } else {
                #[allow(clippy::single_match)]
                match self.probation_queue.write() {
                    Ok(mut probation_queue) => {
                        if let Some(pos) = probation_queue.iter().position(|k| k == key) {
                            probation_queue.remove(pos);
                        }
                    }
                    _ => {}
                }
            }

            // Call eviction callback if provided
            if let Some(callback) = &self.evict_callback {
                callback(key, &entry.value);
            }

            return true;
        }

        false
    }

    fn len(&self) -> usize {
        self.entries
            .read()
            .map(|entries| entries.len())
            .unwrap_or(0)
    }

    fn capacity(&self) -> usize {
        self.capacity
    }

    fn clear(&self) {
        let Ok(mut entries) = self.entries.write() else {
            return;
        };
        let Ok(mut protected_queue) = self.protected_queue.write() else {
            return;
        };
        let Ok(mut probation_queue) = self.probation_queue.write() else {
            return;
        };

        // Call eviction callback for all items
        if let Some(callback) = &self.evict_callback {
            for (key, entry) in entries.iter() {
                callback(key, &entry.value);
            }
        }

        entries.clear();
        protected_queue.clear();
        probation_queue.clear();
    }

    fn close(&self) {
        self.clear();
    }
}

#[cfg(test)]
#[allow(clippy::unwrap_used)]
mod tests {
    use super::*;

    #[test]
    fn test_slru_basic() {
        let cache = SlruCache::new(3, None, None);

        // Add items to probation
        assert!(cache.insert("a", 1));
        assert!(cache.insert("b", 2));
        assert!(cache.insert("c", 3));

        // Access 'a' to promote to protected
        assert_eq!(cache.get(&"a").unwrap().as_ref(), &1);

        // Add new item should evict from probation (b or c)
        assert!(cache.insert("d", 4));

        // 'a' should still be there (in protected)
        assert_eq!(cache.get(&"a").unwrap().as_ref(), &1);
        assert_eq!(cache.len(), 3);
    }

    #[test]
    fn test_slru_promotion() {
        let cache = SlruCache::new(4, None, None);

        // Fill cache
        cache.insert("a", 1);
        cache.insert("b", 2);
        cache.insert("c", 3);
        cache.insert("d", 4);

        // Promote 'a' and 'b' to protected
        cache.get(&"a");
        cache.get(&"b");

        // Add new items - should evict from probation first
        cache.insert("e", 5);
        cache.insert("f", 6);

        // 'a' and 'b' should still exist (protected)
        assert_eq!(cache.get(&"a").unwrap().as_ref(), &1);
        assert_eq!(cache.get(&"b").unwrap().as_ref(), &2);
    }

    #[test]
    fn test_slru_capacity() {
        let cache = SlruCache::<&str, i32>::new(10, None, None);
        assert_eq!(cache.protected_capacity, 8); // 80% of 10
    }

    #[test]
    fn test_slru_eviction() {
        // Test eviction without using a callback
        let cache = SlruCache::new(2, None, None);

        cache.insert("a", 1);
        cache.insert("b", 2);

        // This should evict one of the previous items
        cache.insert("c", 3);

        // Cache size should still be 2
        assert_eq!(cache.len(), 2);

        // Either a or b should be evicted
        let has_a = cache.get(&"a").is_some();
        let has_b = cache.get(&"b").is_some();
        let has_c = cache.get(&"c").is_some();

        // c must exist, and exactly one of a or b
        assert!(has_c);
        assert!(has_a || has_b);
        assert!(!(has_a && has_b));
    }
}
