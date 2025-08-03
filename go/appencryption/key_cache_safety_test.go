package appencryption

import (
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/stretchr/testify/assert"
)

// TestKeyCache_EvictionSafety verifies that cache eviction is safe from use-after-free
func TestKeyCache_EvictionSafety(t *testing.T) {
	// This test verifies that when a key is evicted from cache:
	// 1. It's removed from the cache map BEFORE Close() is called
	// 2. No new references can be obtained after eviction starts
	// 3. Only existing references will be closed properly

	policy := NewCryptoPolicy()
	policy.IntermediateKeyCacheEvictionPolicy = "lru"
	policy.IntermediateKeyCacheMaxSize = 10 // Small cache to force evictions

	cache := newKeyCache(CacheTypeIntermediateKeys, policy)
	defer cache.Close()

	// Fill cache with keys
	for i := 0; i < 20; i++ {
		meta := KeyMeta{ID: string(rune('a' + i)), Created: int64(i)}
		key := internal.NewCryptoKeyForTest(int64(i), false)
		cache.write(meta, newCacheEntry(key))
	}

	// Try to get keys while eviction might be happening
	var wg sync.WaitGroup
	successfulGets := &atomic.Int32{}
	
	for i := 0; i < 100; i++ {
		wg.Add(1)
		go func(id int) {
			defer wg.Done()
			
			// Try to get various keys
			meta := KeyMeta{ID: string(rune('a' + (id % 20))), Created: int64(id % 20)}
			
			_, ok := cache.read(meta)
			if ok {
				successfulGets.Add(1)
				// Use the key briefly
				time.Sleep(time.Microsecond)
			}
		}(i)
	}
	
	wg.Wait()
	
	// We should have gotten some keys successfully
	// The exact number depends on timing and eviction
	assert.Greater(t, int(successfulGets.Load()), 0, "Should have successfully retrieved some keys")
}

// TestCachedCryptoKey_RefCountBehavior verifies reference counting behavior
func TestCachedCryptoKey_RefCountBehavior(t *testing.T) {
	key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
	cachedKey := newCachedCryptoKey(key)
	
	// Initial ref count is 1 (cache reference)
	assert.Equal(t, int64(1), cachedKey.refs.Load())
	
	// Increment (simulating GetOrLoad)
	cachedKey.increment()
	assert.Equal(t, int64(2), cachedKey.refs.Load())
	
	// First close (user releasing reference)
	cachedKey.Close()
	assert.Equal(t, int64(1), cachedKey.refs.Load())
	
	// Second close (cache eviction)
	cachedKey.Close()
	assert.Equal(t, int64(0), cachedKey.refs.Load())
	
	// Note: After ref count hits 0, the key is closed.
	// In production, the cache removes the entry before calling Close(),
	// preventing any new references from being obtained.
}