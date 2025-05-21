#![allow(unused_imports)]

//! Example demonstrating the SLRU (Segmented Least Recently Used) cache implementation
//!
//! This example shows how to use the SLRU cache directly and how it compares to other
//! caching strategies in terms of behavior and performance characteristics.
//!
//! SLRU divides the cache into two segments:
//! - Protected segment: For frequently accessed items (80% of capacity by default)
//! - Probation segment: For newly added or less frequently accessed items (20% of capacity)
//!
//! This design prevents cache pollution from one-time accesses and improves hit rates
//! for frequently accessed items.

use appencryption::cache::SlruCache;
use appencryption::cache::{Cache, CacheBuilder, CachePolicy};
use std::sync::Arc;
use std::time::{Duration, Instant};

fn main() {
    println!("SLRU Cache Example");
    println!("=================");

    // Direct usage of SlruCache
    direct_slru_example();

    // Using CacheBuilder
    builder_example();

    // Comparison with other cache policies
    cache_comparison();

    println!("\nSLRU Cache Example completed successfully!");
}

fn direct_slru_example() {
    println!("\nDirect SLRU Cache Usage:");

    // Create an SLRU cache with capacity 5
    let cache = SlruCache::<String, i32>::new(5, None, None);

    // Add 5 items to fill the cache
    println!("Adding 5 items to the cache...");
    for i in 1..=5 {
        let key = format!("key{}", i);
        cache.insert(key, i);
    }

    // Access some items to promote them to the protected segment
    println!("Accessing key1, key2, key3 to promote them to protected segment...");
    assert_eq!(*cache.get(&"key1".to_string()).unwrap(), 1);
    assert_eq!(*cache.get(&"key2".to_string()).unwrap(), 2);
    assert_eq!(*cache.get(&"key3".to_string()).unwrap(), 3);

    // Add more items - should evict from probation segment first
    println!("Adding 2 more items (key6, key7) - should evict from probation segment...");
    cache.insert("key6".to_string(), 6);
    cache.insert("key7".to_string(), 7);

    // Check what's in the cache now
    let in_cache = vec![
        "key1".to_string(),
        "key2".to_string(),
        "key3".to_string(),
        "key6".to_string(),
        "key7".to_string(),
    ];
    let not_in_cache = vec!["key4".to_string(), "key5".to_string()];

    println!("\nAfter adding new items:");
    for key in &in_cache {
        if let Some(value) = cache.get(key) {
            println!("  {} is in cache with value {}", key, *value);
        } else {
            println!("  {} should be in cache but was evicted!", key);
        }
    }

    for key in &not_in_cache {
        if let Some(_) = cache.get(key) {
            println!("  {} should have been evicted but is still in cache!", key);
        } else {
            println!("  {} was correctly evicted from the cache", key);
        }
    }
}

fn builder_example() {
    println!("\nUsing CacheBuilder:");

    let builder = CacheBuilder::new(5)
        .with_policy(CachePolicy::SLRU)
        .with_ttl(Duration::from_secs(3600))
        .with_evict_callback(|key: &String, value: &i32| {
            println!("  Evicted: {} -> {}", key, value);
        });

    let cache = builder.build();

    println!("Adding 5 items to the cache...");
    for i in 1..=5 {
        let key = format!("key{}", i);
        cache.insert(key, i);
    }

    println!("Cache size: {}", cache.len());

    // Access some items to promote them to protected
    println!("Accessing key1, key2, key3 to promote them to protected segment...");
    assert_eq!(*cache.get(&"key1".to_string()).unwrap(), 1);
    assert_eq!(*cache.get(&"key2".to_string()).unwrap(), 2);
    assert_eq!(*cache.get(&"key3".to_string()).unwrap(), 3);

    // Add more items - should evict from probation first and trigger callback
    println!("Adding more items - should trigger eviction callback for probation segment...");
    cache.insert("key6".to_string(), 6);
    cache.insert("key7".to_string(), 7);

    println!("Final cache size: {}", cache.len());
}

fn cache_comparison() {
    println!("\nCache Policy Comparison:");
    println!("------------------------");

    // Create caches with different policies but same capacity
    let slru_cache = CacheBuilder::new(1000)
        .with_policy(CachePolicy::SLRU)
        .build();

    let lru_cache = CacheBuilder::new(1000)
        .with_policy(CachePolicy::LRU)
        .build();

    let lfu_cache = CacheBuilder::new(1000)
        .with_policy(CachePolicy::LFU)
        .build();

    // Set up test data with a zipf-like distribution
    // A few items are accessed very frequently, most items only once
    let mut keys = Vec::new();

    // Generate 2000 keys but only 20 are "hot" keys
    for i in 0..2000 {
        keys.push(format!("key{}", i));
    }

    let hot_keys: Vec<String> = keys.iter().take(20).cloned().collect();

    // First populate all caches with initial data
    for i in 0..1000 {
        let key = format!("key{}", i);
        slru_cache.insert(key.clone(), i);
        lru_cache.insert(key.clone(), i);
        lfu_cache.insert(key.clone(), i);
    }

    // Now run the test with workload that favors hot keys
    println!("Testing cache policies with skewed workload (zipf-like distribution)...");

    let mut slru_hits = 0;
    let mut lru_hits = 0;
    let mut lfu_hits = 0;

    // Perform 10,000 accesses with zipf-like distribution
    let start = Instant::now();

    for _ in 0..10_000 {
        // 80% chance of accessing a hot key
        let is_hot = rand::random::<f64>() < 0.8;

        let key = if is_hot {
            // Select from hot keys
            hot_keys[rand::random::<usize>() % hot_keys.len()].clone()
        } else {
            // Select from all keys
            keys[rand::random::<usize>() % keys.len()].clone()
        };

        // Access all caches to test hit rates
        if slru_cache.get(&key).is_some() {
            slru_hits += 1;
        }

        if lru_cache.get(&key).is_some() {
            lru_hits += 1;
        }

        if lfu_cache.get(&key).is_some() {
            lfu_hits += 1;
        }
    }

    let elapsed = start.elapsed();

    println!("\nResults after 10,000 accesses with skewed distribution:");
    println!("  SLRU cache: {} hits ({}%)", slru_hits, slru_hits / 100);
    println!("  LRU cache:  {} hits ({}%)", lru_hits, lru_hits / 100);
    println!("  LFU cache:  {} hits ({}%)", lfu_hits, lfu_hits / 100);
    println!("  Time elapsed: {:?}", elapsed);

    println!("\nAnalysis:");
    println!("  SLRU typically performs better than LRU for workloads with");
    println!("  a core set of frequently accessed items, as it protects them");
    println!("  from being evicted by one-time accesses.");
    println!("  LFU can perform well for such workloads but may suffer from");
    println!("  'cache pollution' with items that were frequently accessed");
    println!("  in the past but are no longer used.");
}
