package appencryption

import (
	"context"
	"fmt"
	"sync/atomic"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/stretchr/testify/require"
)

// Concurrent benchmarks with allocation tracking to measure performance under load
// These simulate realistic high-concurrency production scenarios

// BenchmarkSession_Encrypt_Concurrent benchmarks concurrent encryption operations
func BenchmarkSession_Encrypt_Concurrent(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	ctx := context.Background()

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			payload := internal.GetRandBytes(benchmarkPayloadSize)
			_, err := session.Encrypt(ctx, payload)
			if err != nil {
				b.Error(err)
			}
		}
	})
}

// BenchmarkSession_Decrypt_Concurrent benchmarks concurrent decryption operations
func BenchmarkSession_Decrypt_Concurrent(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	// Pre-encrypt data for concurrent decryption
	ctx := context.Background()
	payload := internal.GetRandBytes(benchmarkPayloadSize)
	drr, err := session.Encrypt(ctx, payload)
	require.NoError(b, err)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := session.Decrypt(ctx, *drr)
			if err != nil {
				b.Error(err)
			}
		}
	})
}

// BenchmarkSessionFactory_GetSession_Concurrent benchmarks concurrent session creation
func BenchmarkSessionFactory_GetSession_Concurrent(b *testing.B) {
	b.ReportAllocs()

	factory := newBenchmarkSessionFactory(b)
	defer factory.Close()

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			session, err := factory.GetSession(benchmarkPartitionID)
			if err != nil {
				b.Error(err)
			}
			session.Close()
		}
	})
}

// BenchmarkKeyCache_Concurrent_SameKey benchmarks concurrent access to the same key
func BenchmarkKeyCache_Concurrent_SameKey(b *testing.B) {
	b.ReportAllocs()

	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	keyMeta := KeyMeta{ID: "concurrent_key", Created: time.Now().Unix()}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
				return internal.NewCryptoKeyForTest(meta.Created, false), nil
			})
			if err != nil {
				b.Error(err)
			}
			key.Close()
		}
	})
}

// BenchmarkKeyCache_Concurrent_UniqueKeys benchmarks concurrent access to different keys
func BenchmarkKeyCache_Concurrent_UniqueKeys(b *testing.B) {
	b.ReportAllocs()

	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	var counter int64

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			keyID := fmt.Sprintf("concurrent_key_%d", atomic.AddInt64(&counter, 1))
			keyMeta := KeyMeta{ID: keyID, Created: time.Now().Unix()}

			key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
				return internal.NewCryptoKeyForTest(meta.Created, false), nil
			})
			if err != nil {
				b.Error(err)
			}
			key.Close()
		}
	})
}

// BenchmarkSession_Mixed_Operations_Concurrent benchmarks mixed encrypt/decrypt operations
func BenchmarkSession_Mixed_Operations_Concurrent(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	ctx := context.Background()

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			payload := internal.GetRandBytes(benchmarkPayloadSize)

			// Encrypt
			drr, err := session.Encrypt(ctx, payload)
			if err != nil {
				b.Error(err)
				continue
			}

			// Decrypt
			_, err = session.Decrypt(ctx, *drr)
			if err != nil {
				b.Error(err)
			}
		}
	})
}

// BenchmarkSessionCache_Concurrent benchmarks concurrent session cache operations
func BenchmarkSessionCache_Concurrent(b *testing.B) {
	b.ReportAllocs()

	config := &Config{
		Policy: &CryptoPolicy{
			CacheSessions:       true,
			SessionCacheMaxSize: 1000,
		},
		Product: "benchmark",
		Service: "cache",
	}

	factory := NewSessionFactory(config, &benchmarkMetastore{}, &benchmarkKMS{}, &benchmarkCrypto{}, WithSecretFactory(benchmarkSecretFactory))
	defer factory.Close()

	var counter int64

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			// Use different partition IDs to test cache behavior
			partitionID := fmt.Sprintf("partition_%d", atomic.AddInt64(&counter, 1)%100)

			session, err := factory.GetSession(partitionID)
			if err != nil {
				b.Error(err)
				continue
			}
			session.Close()
		}
	})
}

// BenchmarkCachedCryptoKey_Concurrent_RefCounting benchmarks concurrent reference counting
func BenchmarkCachedCryptoKey_Concurrent_RefCounting(b *testing.B) {
	b.ReportAllocs()

	key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
	cachedKey := newCachedCryptoKey(key)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			// Simulate typical concurrent usage
			cachedKey.increment() // Add reference
			cachedKey.Close()     // Remove reference
		}
	})

	// Final cleanup
	cachedKey.Close()
}

// BenchmarkMemoryPressure_Concurrent_LargePayload benchmarks concurrent performance with larger payloads
func BenchmarkMemoryPressure_Concurrent_LargePayload(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	// Test with larger payloads to understand memory pressure
	largePayloadSize := 64 * 1024 // 64KB
	ctx := context.Background()

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			payload := internal.GetRandBytes(largePayloadSize)

			drr, err := session.Encrypt(ctx, payload)
			if err != nil {
				b.Error(err)
				continue
			}

			_, err = session.Decrypt(ctx, *drr)
			if err != nil {
				b.Error(err)
			}
		}
	})
}
