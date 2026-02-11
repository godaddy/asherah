package appencryption

import (
	"context"
	"fmt"
	"runtime"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

// Memory leak detection tests for Asherah Go implementation
// These tests help identify memory leaks in key caches, sessions, and crypto key reference counting
//
// Usage:
//   go test -run "MemoryLeaks" -v                 # Run all memory leak tests
//   go test -race -run "MemoryLeaks" -v          # Run with race detection
//   go test -run TestKeyCache_MemoryLeaks -v     # Run specific test category
//   go test -run TestGoroutineLeaks -v           # Run goroutine leak tests
//
// Memory leak tests automatically:
// - Force garbage collection before/after measurements
// - Track memory allocation growth with configurable tolerance (5MB default)
// - Detect goroutine leaks with tolerance for background goroutines
// - Test reference counting edge cases
// - Validate session and key cache lifecycle management
//
// Tests are designed to be:
// - Fast (most complete in <1 second)
// - Reliable (tolerant to GC timing and background activity)
// - Comprehensive (cover all major allocation paths)
// - Race-condition safe (all tests pass with -race)

const (
	memLeakTestIterations = 1000
	memLeakToleranceMB    = 5 // MB tolerance for memory growth
)

var memLeakSecretFactory = new(memguard.SecretFactory)

// Create minimal test implementations to avoid import cycles

type benchmarkMetastore struct{}

func (m *benchmarkMetastore) Load(ctx context.Context, keyID string, created int64) (*EnvelopeKeyRecord, error) {
	return nil, nil // Simulate no existing key
}

func (m *benchmarkMetastore) LoadLatest(ctx context.Context, keyID string) (*EnvelopeKeyRecord, error) {
	return nil, nil // Simulate no existing key
}

func (m *benchmarkMetastore) Store(ctx context.Context, keyID string, created int64, envelope *EnvelopeKeyRecord) (bool, error) {
	return true, nil // Simulate successful store
}

type benchmarkKMS struct{}

func (k *benchmarkKMS) EncryptKey(ctx context.Context, key []byte) ([]byte, error) {
	return internal.GetRandBytes(48), nil // Simulated encrypted key
}

func (k *benchmarkKMS) DecryptKey(ctx context.Context, encryptedKey []byte) ([]byte, error) {
	return internal.GetRandBytes(32), nil // Simulated decrypted key
}

func (k *benchmarkKMS) Close() error {
	return nil
}

type benchmarkCrypto struct{}

func (c *benchmarkCrypto) Encrypt(plaintext, key []byte) ([]byte, error) {
	// Simulate encryption overhead by doing some work
	result := make([]byte, len(plaintext)+16) // Add tag
	copy(result, plaintext)
	return result, nil
}

func (c *benchmarkCrypto) Decrypt(ciphertext, key []byte) ([]byte, error) {
	// Simulate decryption by returning the original length
	if len(ciphertext) < 16 {
		return nil, nil
	}
	return ciphertext[:len(ciphertext)-16], nil
}

func (c *benchmarkCrypto) GenerateKey() ([]byte, error) {
	return internal.GetRandBytes(32), nil
}

// memStats represents memory statistics for leak detection
type memStats struct {
	alloc      uint64
	totalAlloc uint64
	sys        uint64
	numGC      uint32
}

// getMemStats returns current memory statistics
func getMemStats() memStats {
	runtime.GC() // Force garbage collection for accurate measurements
	runtime.GC() // Double GC to ensure cleanup

	var m runtime.MemStats
	runtime.ReadMemStats(&m)

	return memStats{
		alloc:      m.Alloc,
		totalAlloc: m.TotalAlloc,
		sys:        m.Sys,
		numGC:      m.NumGC,
	}
}

// checkMemoryLeaks compares memory stats and fails if significant growth is detected
func checkMemoryLeaks(t *testing.T, before, after memStats, testName string) {
	var allocGrowthMB float64
	if after.alloc >= before.alloc {
		allocGrowthMB = float64(after.alloc-before.alloc) / 1024 / 1024
	} else {
		// Handle case where memory decreased (GC freed more than we allocated)
		allocGrowthMB = -float64(before.alloc-after.alloc) / 1024 / 1024
	}

	t.Logf("%s Memory Stats:", testName)
	t.Logf("  Alloc growth: %.2f MB", allocGrowthMB)
	t.Logf("  TotalAlloc growth: %.2f MB", float64(after.totalAlloc-before.totalAlloc)/1024/1024)
	t.Logf("  Sys growth: %.2f MB", float64(after.sys-before.sys)/1024/1024)
	t.Logf("  GC runs: %d", after.numGC-before.numGC)

	if allocGrowthMB > memLeakToleranceMB {
		t.Errorf("Potential memory leak detected in %s: %.2f MB growth (tolerance: %d MB)",
			testName, allocGrowthMB, memLeakToleranceMB)
	}
}

// TestKeyCache_MemoryLeaks tests for memory leaks in key cache operations
func TestKeyCache_MemoryLeaks(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T, cache *keyCache)
	}{
		{
			name: "GetOrLoad_SameKey",
			testFunc: func(t *testing.T, cache *keyCache) {
				keyMeta := KeyMeta{ID: "leak_test_key", Created: time.Now().Unix()}

				for i := 0; i < memLeakTestIterations; i++ {
					key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
						return internal.NewCryptoKeyForTest(meta.Created, false), nil
					})
					require.NoError(t, err)
					key.Close()
				}
			},
		},
		{
			name: "GetOrLoad_UniqueKeys",
			testFunc: func(t *testing.T, cache *keyCache) {
				for i := 0; i < memLeakTestIterations; i++ {
					keyMeta := KeyMeta{ID: fmt.Sprintf("leak_test_key_%d", i), Created: time.Now().Unix()}

					key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
						return internal.NewCryptoKeyForTest(meta.Created, false), nil
					})
					require.NoError(t, err)
					key.Close()
				}
			},
		},
		{
			name: "GetOrLoadLatest_SameKey",
			testFunc: func(t *testing.T, cache *keyCache) {
				keyID := "leak_test_latest_key"

				for i := 0; i < memLeakTestIterations; i++ {
					key, err := cache.GetOrLoadLatest(keyID, func(meta KeyMeta) (*internal.CryptoKey, error) {
						return internal.NewCryptoKeyForTest(time.Now().Unix(), false), nil
					})
					require.NoError(t, err)
					key.Close()
				}
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
			defer cache.Close()

			before := getMemStats()
			tt.testFunc(t, cache)
			after := getMemStats()

			checkMemoryLeaks(t, before, after, fmt.Sprintf("KeyCache_%s", tt.name))
		})
	}
}

// TestCachedCryptoKey_ReferenceCountingLeaks tests for leaks in reference counting
func TestCachedCryptoKey_ReferenceCountingLeaks(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T)
	}{
		{
			name: "Increment_Decrement_Cycles",
			testFunc: func(t *testing.T) {
				for i := 0; i < memLeakTestIterations; i++ {
					key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
					cachedKey := newCachedCryptoKey(key)

					// Simulate reference counting cycles
					cachedKey.increment()
					cachedKey.increment()
					cachedKey.Close() // -1
					cachedKey.Close() // -1
					cachedKey.Close() // -1 (should trigger key.Close())
				}
			},
		},
		{
			name: "Multiple_References_Same_Key",
			testFunc: func(t *testing.T) {
				key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
				cachedKey := newCachedCryptoKey(key)

				for i := 0; i < memLeakTestIterations; i++ {
					cachedKey.increment()
					cachedKey.Close()
				}

				// Final cleanup
				cachedKey.Close()
			},
		},
		{
			name: "Create_Close_Cycle",
			testFunc: func(t *testing.T) {
				for i := 0; i < memLeakTestIterations; i++ {
					key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
					cachedKey := newCachedCryptoKey(key)
					cachedKey.Close()
				}
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			before := getMemStats()
			tt.testFunc(t)
			after := getMemStats()

			checkMemoryLeaks(t, before, after, fmt.Sprintf("CachedCryptoKey_%s", tt.name))
		})
	}
}

// TestSessionFactory_MemoryLeaks tests for memory leaks in session factory operations
func TestSessionFactory_MemoryLeaks(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T, factory *SessionFactory)
	}{
		{
			name: "GetSession_Close_Cycle",
			testFunc: func(t *testing.T, factory *SessionFactory) {
				partitionID := "leak_test_partition"

				for i := 0; i < memLeakTestIterations; i++ {
					session, err := factory.GetSession(partitionID)
					require.NoError(t, err)
					session.Close()
				}
			},
		},
		{
			name: "Multiple_Partitions",
			testFunc: func(t *testing.T, factory *SessionFactory) {
				for i := 0; i < memLeakTestIterations; i++ {
					partitionID := fmt.Sprintf("leak_test_partition_%d", i)
					session, err := factory.GetSession(partitionID)
					require.NoError(t, err)
					session.Close()
				}
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			config := &Config{
				Policy:  NewCryptoPolicy(),
				Product: "leak_test",
				Service: "test",
			}

			factory := NewSessionFactory(
				config,
				&benchmarkMetastore{},
				&benchmarkKMS{},
				&benchmarkCrypto{},
				WithSecretFactory(memLeakSecretFactory),
			)
			defer factory.Close()

			before := getMemStats()
			tt.testFunc(t, factory)
			after := getMemStats()

			checkMemoryLeaks(t, before, after, fmt.Sprintf("SessionFactory_%s", tt.name))
		})
	}
}

// TestSession_EncryptDecrypt_MemoryLeaks tests for memory leaks in encrypt/decrypt operations
func TestSession_EncryptDecrypt_MemoryLeaks(t *testing.T) {
	config := &Config{
		Policy:  NewCryptoPolicy(),
		Product: "leak_test",
		Service: "test",
	}

	factory := NewSessionFactory(
		config,
		&benchmarkMetastore{},
		&benchmarkKMS{},
		&benchmarkCrypto{},
		WithSecretFactory(memLeakSecretFactory),
	)
	defer factory.Close()

	session, err := factory.GetSession("leak_test_partition")
	require.NoError(t, err)
	defer session.Close()

	ctx := context.Background()
	payload := internal.GetRandBytes(1024)

	before := getMemStats()

	// Perform many encrypt/decrypt cycles
	for i := 0; i < memLeakTestIterations; i++ {
		drr, err := session.Encrypt(ctx, payload)
		require.NoError(t, err)

		decrypted, err := session.Decrypt(ctx, *drr)
		require.NoError(t, err)
		require.Equal(t, len(payload), len(decrypted))
	}

	after := getMemStats()
	checkMemoryLeaks(t, before, after, "Session_EncryptDecrypt")
}

// testGoroutineLeaks runs a test and checks for goroutine leaks
func testGoroutineLeaks(t *testing.T, name string, testFunc func(t *testing.T)) {
	before := runtime.NumGoroutine()
	testFunc(t)

	// Allow time for goroutines to cleanup
	runtime.GC()
	time.Sleep(100 * time.Millisecond)

	after := runtime.NumGoroutine()
	goroutineGrowth := after - before

	t.Logf("Goroutine growth: %d (before: %d, after: %d)", goroutineGrowth, before, after)

	// Allow some tolerance for background goroutines
	if goroutineGrowth > 5 {
		t.Errorf("Potential goroutine leak detected in %s: %d new goroutines", name, goroutineGrowth)
	}
}

// TestGoroutineLeaks tests for goroutine leaks
func TestGoroutineLeaks(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T)
	}{
		{
			name: "SessionFactory_Creation_Cleanup",
			testFunc: func(t *testing.T) {
				for i := 0; i < 100; i++ { // Fewer iterations for goroutine tests
					config := &Config{
						Policy:  NewCryptoPolicy(),
						Product: "goroutine_test",
						Service: "test",
					}

					factory := NewSessionFactory(
						config,
						&benchmarkMetastore{},
						&benchmarkKMS{},
						&benchmarkCrypto{},
						WithSecretFactory(memLeakSecretFactory),
					)

					// Create and close some sessions
					session, err := factory.GetSession("test_partition")
					require.NoError(t, err)
					session.Close()

					factory.Close()
				}
			},
		},
		{
			name: "KeyCache_Concurrent_Access",
			testFunc: func(t *testing.T) {
				cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
				defer cache.Close()

				keyMeta := KeyMeta{ID: "goroutine_test_key", Created: time.Now().Unix()}

				// Create many concurrent operations
				for i := 0; i < 100; i++ {
					go func() {
						key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
							return internal.NewCryptoKeyForTest(meta.Created, false), nil
						})
						if err == nil {
							key.Close()
						}
					}()
				}

				// Allow goroutines to complete
				time.Sleep(100 * time.Millisecond)
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			testGoroutineLeaks(t, tt.name, tt.testFunc)
		})
	}
}

// TestMemoryLeaks_WithCache tests memory leaks when session caching is enabled
func TestMemoryLeaks_WithCache(t *testing.T) {
	config := &Config{
		Policy: &CryptoPolicy{
			CacheSessions:              true,
			SessionCacheMaxSize:        100,
			SharedIntermediateKeyCache: true,
		},
		Product: "cache_leak_test",
		Service: "test",
	}

	factory := NewSessionFactory(
		config,
		&benchmarkMetastore{},
		&benchmarkKMS{},
		&benchmarkCrypto{},
		WithSecretFactory(memLeakSecretFactory),
	)
	defer factory.Close()

	before := getMemStats()

	// Create many sessions that should be cached
	partitions := make(map[string]bool)
	for i := 0; i < memLeakTestIterations; i++ {
		partitionID := fmt.Sprintf("cache_test_%d", i%50) // Reuse 50 partitions
		partitions[partitionID] = true

		session, err := factory.GetSession(partitionID)
		require.NoError(t, err)
		session.Close()
	}

	after := getMemStats()

	t.Logf("Created sessions for %d unique partitions", len(partitions))
	checkMemoryLeaks(t, before, after, "SessionCache")
}

// TestMemoryLeaks_LargePayloads tests memory leaks with large payloads
func TestMemoryLeaks_LargePayloads(t *testing.T) {
	config := &Config{
		Policy:  NewCryptoPolicy(),
		Product: "large_payload_test",
		Service: "test",
	}

	factory := NewSessionFactory(
		config,
		&benchmarkMetastore{},
		&benchmarkKMS{},
		&benchmarkCrypto{},
		WithSecretFactory(memLeakSecretFactory),
	)
	defer factory.Close()

	session, err := factory.GetSession("large_payload_partition")
	require.NoError(t, err)
	defer session.Close()

	ctx := context.Background()
	largePayload := internal.GetRandBytes(64 * 1024) // 64KB

	before := getMemStats()

	// Encrypt/decrypt large payloads multiple times
	for i := 0; i < 100; i++ { // Fewer iterations due to large payload size
		drr, err := session.Encrypt(ctx, largePayload)
		require.NoError(t, err)

		decrypted, err := session.Decrypt(ctx, *drr)
		require.NoError(t, err)
		require.Equal(t, len(largePayload), len(decrypted))
	}

	after := getMemStats()
	checkMemoryLeaks(t, before, after, "LargePayloads")
}

// TestMemoryLeaks_ReferenceCountingEdgeCases tests edge cases in reference counting
func TestMemoryLeaks_ReferenceCountingEdgeCases(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T)
	}{
		{
			name: "Double_Close",
			testFunc: func(t *testing.T) {
				for i := 0; i < memLeakTestIterations/10; i++ {
					key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
					cachedKey := newCachedCryptoKey(key)

					// Close multiple times (should be safe)
					cachedKey.Close()
					cachedKey.Close() // Should be no-op
					cachedKey.Close() // Should be no-op
				}
			},
		},
		{
			name: "Increment_After_Close",
			testFunc: func(t *testing.T) {
				for i := 0; i < memLeakTestIterations/10; i++ {
					key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
					cachedKey := newCachedCryptoKey(key)

					cachedKey.Close() // Ref count goes to 0, key is closed

					// This should still work but key is already closed
					// This tests that we don't leak memory even in edge cases
					cachedKey.increment()
					cachedKey.Close()
				}
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			before := getMemStats()
			tt.testFunc(t)
			after := getMemStats()

			checkMemoryLeaks(t, before, after, fmt.Sprintf("ReferenceCountingEdgeCase_%s", tt.name))
		})
	}
}

// BenchmarkMemoryLeaks_SessionOperations provides benchmarks that can detect memory leaks over time
func BenchmarkMemoryLeaks_SessionOperations(b *testing.B) {
	config := &Config{
		Policy:  NewCryptoPolicy(),
		Product: "benchmark_leak_test",
		Service: "test",
	}

	factory := NewSessionFactory(
		config,
		&benchmarkMetastore{},
		&benchmarkKMS{},
		&benchmarkCrypto{},
		WithSecretFactory(memLeakSecretFactory),
	)
	defer factory.Close()

	session, err := factory.GetSession("benchmark_partition")
	require.NoError(b, err)
	defer session.Close()

	ctx := context.Background()
	payload := internal.GetRandBytes(1024)

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		drr, err := session.Encrypt(ctx, payload)
		if err != nil {
			b.Fatal(err)
		}

		_, err = session.Decrypt(ctx, *drr)
		if err != nil {
			b.Fatal(err)
		}
	}
}
