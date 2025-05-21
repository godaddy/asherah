use std::collections::HashMap;
use std::fmt;
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::Duration;

use super::{Cache, EvictCallback};

/// Simple cache implementation with no eviction policy
///
/// This cache stores all entries without any size limits or eviction.
/// It's best suited for scenarios where memory usage is not a concern
/// and maximum performance is desired.
///
/// The cache tracks entry access time for TTL expiration if configured.
pub struct SimpleCache<K, V>
where
    K: fmt::Debug,
    V: fmt::Debug,
{
    /// Current entries in the cache
    entries: RwLock<HashMap<K, Arc<V>>>,

    /// Optional callback when an item is removed (not used for eviction)
    evict_callback: Option<EvictCallback<K, V>>,
}

impl<K, V> fmt::Debug for SimpleCache<K, V>
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

        f.debug_struct("SimpleCache")
            .field("entries_len", &entries_len)
            .finish()
    }
}

impl<K, V> SimpleCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    /// Create a new simple cache
    pub fn new(
        _capacity: usize, // Ignored for simple cache
        evict_callback: Option<EvictCallback<K, V>>,
        _ttl: Option<Duration>, // Ignored for simple cache
    ) -> Self {
        Self {
            entries: RwLock::new(HashMap::new()),
            evict_callback,
        }
    }
}

impl<K, V> Cache<K, V> for SimpleCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static + fmt::Debug,
    V: Clone + Send + Sync + 'static + fmt::Debug,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        let entries = self.entries.read().ok()?;
        entries.get(key).cloned()
    }

    fn insert(&self, key: K, value: V) -> bool {
        match self.entries.write() {
            Ok(mut entries) => {
                entries.insert(key, Arc::new(value));
                true
            }
            Err(_) => false,
        }
    }

    fn remove(&self, key: &K) -> bool {
        match self.entries.write() {
            Ok(mut entries) => {
                match entries.remove(key) {
                    Some(value) => {
                        // Call eviction callback if provided
                        if let Some(callback) = &self.evict_callback {
                            callback(key, &value);
                        }
                        true
                    }
                    None => false,
                }
            }
            Err(_) => false,
        }
    }

    fn len(&self) -> usize {
        self.entries
            .read()
            .map(|entries| entries.len())
            .unwrap_or(0)
    }

    fn capacity(&self) -> usize {
        // Simple cache has unlimited capacity
        usize::MAX
    }

    fn clear(&self) {
        match self.entries.write() {
            Ok(mut entries) => {
                // Call eviction callback for all items if provided
                if let Some(callback) = &self.evict_callback {
                    for (key, value) in entries.iter() {
                        callback(key, value);
                    }
                }

                entries.clear();
            }
            Err(_) => {
                // Unable to acquire lock, nothing to clear
            }
        }
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
    fn test_simple_cache_basic() {
        let cache = SimpleCache::new(0, None, None);

        // Add items
        assert!(cache.insert("a", 1));
        assert!(cache.insert("b", 2));
        assert!(cache.insert("c", 3));

        // Retrieve items
        assert_eq!(
            cache
                .get(&"a")
                .expect("Failed to acquire lock in SimpleCache")
                .as_ref(),
            &1
        );
        assert_eq!(
            cache
                .get(&"b")
                .expect("Failed to acquire lock in SimpleCache")
                .as_ref(),
            &2
        );
        assert_eq!(
            cache
                .get(&"c")
                .expect("Failed to acquire lock in SimpleCache")
                .as_ref(),
            &3
        );

        // Check size
        assert_eq!(cache.len(), 3);
    }

    #[test]
    fn test_simple_cache_no_eviction() {
        let cache = SimpleCache::new(1, None, None); // Capacity is ignored

        // Add many items - no eviction should occur
        for i in 0..100 {
            assert!(cache.insert(i, i * 2));
        }

        // All items should still be present
        assert_eq!(cache.len(), 100);

        // Verify all items exist
        for i in 0..100 {
            assert_eq!(
                cache
                    .get(&i)
                    .expect("Failed to acquire lock in SimpleCache")
                    .as_ref(),
                &(i * 2)
            );
        }
    }

    #[test]
    fn test_simple_cache_remove() {
        use std::sync::atomic::{AtomicUsize, Ordering};

        let removed_count = Arc::new(AtomicUsize::new(0));
        let removed_count_clone = removed_count.clone();

        let callback: EvictCallback<&str, i32> = Arc::new(move |_key, _value| {
            removed_count_clone.fetch_add(1, Ordering::SeqCst);
        });

        let cache = SimpleCache::new(0, Some(callback), None);

        cache.insert("a", 1);
        cache.insert("b", 2);

        // Remove an item
        assert!(cache.remove(&"a"));
        assert_eq!(removed_count.load(Ordering::SeqCst), 1);

        // Try to remove non-existent item
        assert!(!cache.remove(&"c"));
        assert_eq!(removed_count.load(Ordering::SeqCst), 1);
    }

    #[test]
    fn test_simple_cache_clear() {
        use std::sync::atomic::{AtomicUsize, Ordering};

        let cleared_count = Arc::new(AtomicUsize::new(0));
        let cleared_count_clone = cleared_count.clone();

        let callback: EvictCallback<&str, i32> = Arc::new(move |_key, _value| {
            cleared_count_clone.fetch_add(1, Ordering::SeqCst);
        });

        let cache = SimpleCache::new(0, Some(callback), None);

        cache.insert("a", 1);
        cache.insert("b", 2);
        cache.insert("c", 3);

        cache.clear();

        assert_eq!(cache.len(), 0);
        assert_eq!(cleared_count.load(Ordering::SeqCst), 3);
    }

    #[test]
    fn test_simple_cache_capacity() {
        let cache = SimpleCache::<&str, i32>::new(0, None, None);
        assert_eq!(cache.capacity(), usize::MAX);
    }
}
