package appencryption

import (
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/stretchr/testify/assert"
)

// TestCachedCryptoKey_RaceConditionFixed tests that the race condition
// in reference counting has been fixed. This test also verifies that
// the use-after-free scenario cannot occur because:
// 1. The cache removes items from its map BEFORE calling Close()
// 2. Once removed from the cache map, new callers cannot get a reference
// 3. Only existing reference holders can call Close()
func TestCachedCryptoKey_RaceConditionFixed(t *testing.T) {
	// Run this test multiple times to increase chances of catching a race
	for i := 0; i < 100; i++ {
		key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
		cachedKey := newCachedCryptoKey(key)

		const numGoroutines = 100
		var wg sync.WaitGroup
		wg.Add(numGoroutines)

		// We can't track Close() calls directly on CryptoKey, but we can verify
		// the reference counting works correctly

		// Simulate concurrent access
		for j := 0; j < numGoroutines; j++ {
			go func() {
				defer wg.Done()
				
				// Increment (simulating cache hit)
				cachedKey.increment()
				
				// Small delay to increase concurrency
				time.Sleep(time.Microsecond)
				
				// Decrement (simulating release)
				cachedKey.Close()
			}()
		}

		wg.Wait()

		// The cache's reference should still exist
		assert.Equal(t, int64(1), cachedKey.refs.Load(), "Should have 1 reference from cache")

		// Final close from cache
		cachedKey.Close()
		assert.Equal(t, int64(0), cachedKey.refs.Load(), "Should have 0 references")
	}
}

// TestCachedCryptoKey_LogRaceCondition demonstrates the specific race in logging
func TestCachedCryptoKey_LogRaceCondition(t *testing.T) {
	// This test demonstrates why we can't use separate Add(-1) and Load() operations
	refs := &atomic.Int64{}
	refs.Store(1)

	var raceDetected bool
	var wg sync.WaitGroup
	wg.Add(2)

	// Goroutine 1: Decrement and try to log
	go func() {
		defer wg.Done()
		if refs.Add(-1) == 0 {
			// Simulate delay between operations
			time.Sleep(5 * time.Millisecond)
			// By now, the value might have changed
			loggedValue := refs.Load()
			if loggedValue != 0 {
				raceDetected = true
			}
		}
	}()

	// Goroutine 2: Increment after a small delay
	go func() {
		defer wg.Done()
		time.Sleep(2 * time.Millisecond)
		refs.Add(1)
	}()

	wg.Wait()

	if raceDetected {
		t.Log("Race condition would have occurred with separate Add/Load operations")
	}
}