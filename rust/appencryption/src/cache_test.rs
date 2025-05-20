#[cfg(test)]
mod simple_cache_test {
    use crate::cache::simple::SimpleCache;
    use crate::cache::Cache;
    use std::sync::Arc;
    use std::time::Duration;
    
    #[test]
    fn test_simple_cache_basic() {
        let cache = SimpleCache::<String, i32>::new(100, None, None);
        
        // Test basic operations
        assert!(cache.insert("key1".to_string(), 42));
        assert_eq!(cache.get(&"key1".to_string()).unwrap().as_ref(), &42);
        
        // Test replacement
        assert!(cache.insert("key1".to_string(), 100));
        assert_eq!(cache.get(&"key1".to_string()).unwrap().as_ref(), &100);
    }
}