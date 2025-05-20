use std::collections::{HashMap, VecDeque};
use std::hash::Hash;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use super::{Cache, EvictCallback};

/// Protected segment ratio for SLRU
const PROTECTED_RATIO: f64 = 0.8;

/// Entry in the SLRU cache with metadata
struct SlruEntry<V> {
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
pub struct SlruCache<K, V> {
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

impl<K, V> SlruCache<K, V>
where
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
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
        let mut entries = self.entries.write().unwrap();
        let mut probation_queue = self.probation_queue.write().unwrap();
        let mut protected_queue = self.protected_queue.write().unwrap();
        
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
        let mut probation_queue = self.probation_queue.write().unwrap();
        let mut protected_queue = self.protected_queue.write().unwrap();
        let mut entries = self.entries.write().unwrap();
        
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
        let mut entries = self.entries.write().unwrap();
        let entry = match entries.get_mut(key) {
            Some(e) => e,
            None => return,
        };
        
        entry.last_accessed = Instant::now();
        let protected = entry.protected;
        drop(entries);
        
        if protected {
            // Already in protected segment, just update position
            let mut protected_queue = self.protected_queue.write().unwrap();
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
    K: Eq + Hash + Clone + Send + Sync + 'static,
    V: Clone + Send + Sync + 'static,
{
    fn get(&self, key: &K) -> Option<Arc<V>> {
        let entries = self.entries.read().unwrap();
        
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
        let mut entries = self.entries.write().unwrap();
        
        // Check if we need to evict
        if entries.len() >= self.capacity && !entries.contains_key(&key) {
            drop(entries);
            self.evict();
            entries = self.entries.write().unwrap();
        }
        
        // Update existing entry
        if let Some(entry) = entries.get_mut(&key) {
            entry.value = Arc::new(value);
            entry.last_accessed = Instant::now();
            let protected = entry.protected;
            drop(entries);
            
            if protected {
                let mut protected_queue = self.protected_queue.write().unwrap();
                if let Some(pos) = protected_queue.iter().position(|k| k == &key) {
                    protected_queue.remove(pos);
                }
                protected_queue.push_front(key);
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
        
        let mut probation_queue = self.probation_queue.write().unwrap();
        probation_queue.push_front(key);
        
        true
    }
    
    fn remove(&self, key: &K) -> bool {
        let mut entries = self.entries.write().unwrap();
        
        if let Some(entry) = entries.remove(key) {
            if entry.protected {
                let mut protected_queue = self.protected_queue.write().unwrap();
                if let Some(pos) = protected_queue.iter().position(|k| k == key) {
                    protected_queue.remove(pos);
                }
            } else {
                let mut probation_queue = self.probation_queue.write().unwrap();
                if let Some(pos) = probation_queue.iter().position(|k| k == key) {
                    probation_queue.remove(pos);
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
        self.entries.read().unwrap().len()
    }
    
    fn capacity(&self) -> usize {
        self.capacity
    }
    
    fn clear(&self) {
        let mut entries = self.entries.write().unwrap();
        let mut protected_queue = self.protected_queue.write().unwrap();
        let mut probation_queue = self.probation_queue.write().unwrap();
        
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
    fn test_slru_eviction_callback() {
        use std::sync::atomic::{AtomicUsize, Ordering};
        
        let evicted_count = Arc::new(AtomicUsize::new(0));
        let evicted_count_clone = evicted_count.clone();
        
        let callback: EvictCallback<&str, i32> = Arc::new(move |_key, _value| {
            evicted_count_clone.fetch_add(1, Ordering::SeqCst);
        });
        
        let cache = SlruCache::new(2, Some(callback), None);
        
        cache.insert("a", 1);
        cache.insert("b", 2);
        cache.insert("c", 3); // Should trigger eviction
        
        assert_eq!(evicted_count.load(Ordering::SeqCst), 1);
    }
}