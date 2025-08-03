package appencryption

import (
	"context"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/stretchr/testify/require"
)

// Hot path benchmarks with allocation tracking for performance monitoring
// These benchmarks focus on the most frequently used operations in production systems

const (
	benchmarkPartitionID = "benchmark_partition"
	benchmarkPayloadSize = 1024 // 1KB payload for realistic testing
)

var (
	benchmarkSecretFactory = new(memguard.SecretFactory)
)

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

// Remove GenerateDataKey as it's not part of the interface

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

// Helper functions for creating test instances

func newBenchmarkSessionFactory(b *testing.B) *SessionFactory {
	config := &Config{
		Policy:  NewCryptoPolicy(),
		Product: "benchmark",
		Service: "test",
	}
	
	return NewSessionFactory(
		config,
		&benchmarkMetastore{},
		&benchmarkKMS{},
		&benchmarkCrypto{},
		WithSecretFactory(benchmarkSecretFactory),
	)
}

func newBenchmarkSession(b *testing.B) *Session {
	factory := newBenchmarkSessionFactory(b)
	session, err := factory.GetSession(benchmarkPartitionID)
	require.NoError(b, err)
	return session
}

// BenchmarkSessionFactory_GetSession_HotPath benchmarks the hot path of getting a session
// This is one of the most critical operations as it's called for every encrypt/decrypt
func BenchmarkSessionFactory_GetSession_HotPath(b *testing.B) {
	b.ReportAllocs()

	factory := newBenchmarkSessionFactory(b)
	defer factory.Close()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		session, err := factory.GetSession(benchmarkPartitionID)
		if err != nil {
			b.Fatal(err)
		}
		session.Close()
	}
}

// BenchmarkSessionFactory_GetSession_Cached benchmarks session retrieval when cached
func BenchmarkSessionFactory_GetSession_Cached(b *testing.B) {
	b.ReportAllocs()

	config := &Config{
		Policy: &CryptoPolicy{
			CacheSessions:              true,
			SessionCacheMaxSize:        1000,
			SharedIntermediateKeyCache: true,
		},
		Product: "benchmark",
		Service: "test",
	}

	factory := NewSessionFactory(config, &benchmarkMetastore{}, &benchmarkKMS{}, &benchmarkCrypto{})
	defer factory.Close()

	// Pre-warm the cache
	session, err := factory.GetSession(benchmarkPartitionID)
	require.NoError(b, err)
	session.Close()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		session, err := factory.GetSession(benchmarkPartitionID)
		if err != nil {
			b.Fatal(err)
		}
		session.Close()
	}
}

// BenchmarkSession_Encrypt_HotPath benchmarks the critical encrypt operation
func BenchmarkSession_Encrypt_HotPath(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	payload := internal.GetRandBytes(benchmarkPayloadSize)
	ctx := context.Background()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		_, err := session.Encrypt(ctx, payload)
		if err != nil {
			b.Fatal(err)
		}
	}
}

// BenchmarkSession_Decrypt_HotPath benchmarks the critical decrypt operation
func BenchmarkSession_Decrypt_HotPath(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	payload := internal.GetRandBytes(benchmarkPayloadSize)
	ctx := context.Background()

	// Pre-encrypt the data for decryption benchmark
	drr, err := session.Encrypt(ctx, payload)
	require.NoError(b, err)

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		_, err := session.Decrypt(ctx, *drr)
		if err != nil {
			b.Fatal(err)
		}
	}
}

// BenchmarkSession_EncryptDecrypt_RoundTrip benchmarks full round-trip operation
func BenchmarkSession_EncryptDecrypt_RoundTrip(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	ctx := context.Background()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		payload := internal.GetRandBytes(benchmarkPayloadSize)

		drr, err := session.Encrypt(ctx, payload)
		if err != nil {
			b.Fatal(err)
		}

		decrypted, err := session.Decrypt(ctx, *drr)
		if err != nil {
			b.Fatal(err)
		}

		if len(decrypted) != len(payload) {
			b.Fatal("payload size mismatch")
		}
	}
}

// BenchmarkKeyCache_GetOrLoad_WithAllocation benchmarks key cache with allocation tracking
func BenchmarkKeyCache_GetOrLoad_WithAllocation(b *testing.B) {
	b.ReportAllocs()

	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	keyMeta := KeyMeta{ID: "benchmark_key", Created: time.Now().Unix()}

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
			return internal.NewCryptoKeyForTest(meta.Created, false), nil
		})
		if err != nil {
			b.Fatal(err)
		}
		key.Close()
	}
}

// BenchmarkKeyCache_GetOrLoadLatest_WithAllocation benchmarks latest key retrieval
func BenchmarkKeyCache_GetOrLoadLatest_WithAllocation(b *testing.B) {
	b.ReportAllocs()

	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		key, err := cache.GetOrLoadLatest("benchmark_key", func(meta KeyMeta) (*internal.CryptoKey, error) {
			return internal.NewCryptoKeyForTest(time.Now().Unix(), false), nil
		})
		if err != nil {
			b.Fatal(err)
		}
		key.Close()
	}
}

// BenchmarkCachedCryptoKey_Operations_WithAllocation benchmarks key reference operations
func BenchmarkCachedCryptoKey_Operations_WithAllocation(b *testing.B) {
	b.ReportAllocs()

	key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
	cachedKey := newCachedCryptoKey(key)

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		// Simulate typical usage pattern
		cachedKey.increment() // Get reference
		cachedKey.Close()     // Release reference
	}

	// Final cleanup
	cachedKey.Close()
}

// BenchmarkMemoryPressure_LargePayload benchmarks performance with larger payloads
func BenchmarkMemoryPressure_LargePayload(b *testing.B) {
	b.ReportAllocs()

	session := newBenchmarkSession(b)
	defer session.Close()

	// Test with larger payloads to understand memory pressure
	largePayloadSize := 64 * 1024 // 64KB
	ctx := context.Background()

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		payload := internal.GetRandBytes(largePayloadSize)

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