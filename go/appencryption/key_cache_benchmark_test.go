package appencryption

import (
	"flag"
	"fmt"
	"sync/atomic"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

var (
	secretFactory = new(memguard.SecretFactory)
	created       = time.Now().Unix()
	enableDebug   = flag.Bool("debug", false, "enable debug logging")
)

func ConfigureLogging() {
	if *enableDebug {
		log.SetLogger(logger{})
	}
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsReadExistingKey(b *testing.B) {
	ConfigureLogging()

	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	c.keys.Set(cacheKey(testKey, created), cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: internal.NewCryptoKeyForTest(created, false)},
		loadedAt: time.Now(),
	})

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := c.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, errors.New("loader should not be executed")
			})

			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsWriteSameKey(b *testing.B) {
	ConfigureLogging()

	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				return internal.NewCryptoKeyForTest(created, false), nil
			})

			assert.NoError(b, err)

			latest, _ := c.getLatestKeyMeta(testKey)
			latestKey := cacheKey(latest.ID, latest.Created)

			assert.Equal(b, created, c.keys.GetOrPanic(latestKey).key.Created())
		}
	})
}

type logger struct{}

func (logger) Debugf(format string, v ...interface{}) {
	fmt.Printf(format, v...)
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsWriteUniqueKeys(b *testing.B) {
	ConfigureLogging()

	var (
		c = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		i int64
	)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.AddInt64(&i, 1) - 1

			loader := func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				return internal.NewCryptoKeyForTest(created, false), nil
			}

			keyID := fmt.Sprintf("%s-%d", testKey, curr)

			_, err := c.GetOrLoad(KeyMeta{keyID, created}, loader)
			assert.NoError(b, err)

			// ensure we have a "latest" entry for this key as well
			latest, err := c.GetOrLoadLatest(keyID, loader)
			assert.NoError(b, err)
			assert.NotNil(b, latest)
		}
	})
	assert.NotNil(b, c.keys)

	expected := i
	if expected > DefaultKeyCacheMaxSize {
		expected = DefaultKeyCacheMaxSize
	}

	assert.Equal(b, expected, int64(c.keys.Len()))
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsReadRevokedKey(b *testing.B) {
	var (
		c       = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))

	assert.NoError(b, err)

	cacheEntry := cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: key},
		loadedAt: time.Unix(created, 0),
	}

	defer c.Close()
	c.keys.Set(cacheKey(testKey, created), cacheEntry)
	c.mapLatestKeyMeta(testKey, KeyMeta{testKey, created})

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				return internal.NewCryptoKey(secretFactory, created, true, []byte("testing"))
			})

			assert.NoError(b, err)

			latest, _ := c.getLatestKeyMeta(testKey)
			latestKey := cacheKey(latest.ID, latest.Created)
			assert.Equal(b, created, c.keys.GetOrPanic(latestKey).key.Created())
			assert.True(b, c.keys.GetOrPanic(latestKey).key.Revoked())
			assert.True(b, c.keys.GetOrPanic(cacheKey(testKey, created)).key.Revoked())
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsRead_NeedReloadKey(b *testing.B) {
	var (
		c       = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))

	assert.NoError(b, err)

	cacheEntry := cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: key},
		loadedAt: time.Unix(created, 0),
	}

	defer c.Close()

	c.keys.Set(cacheKey(testKey, created), cacheEntry)
	c.mapLatestKeyMeta(testKey, KeyMeta{testKey, created})

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			k, err := c.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (*internal.CryptoKey, error) {
				// Note: this function should only happen on first load (although could execute more than once currently), if it doesn't, then something is broken
				return internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))
			})
			if err != nil {
				b.Error(err)
			}

			if created != k.Created() {
				b.Error("created mismatch")
			}
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsReadUniqueKeys(b *testing.B) {
	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	for i := 0; i < b.N && i < DefaultKeyCacheMaxSize; i++ {
		keyID := fmt.Sprintf(testKey+"-%d", i)
		meta := KeyMeta{ID: keyID, Created: created}

		c.mapLatestKeyMeta(meta.ID, meta)
		c.keys.Set(cacheKey(meta.ID, meta.Created), cacheEntry{
			key:      &cachedCryptoKey{CryptoKey: internal.NewCryptoKeyForTest(created, false), refs: 1},
			loadedAt: time.Now(),
		})
	}

	i := atomic.Int64{}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := i.Add(1) - 1
			curr = curr % DefaultKeyCacheMaxSize

			id := fmt.Sprintf(testKey+"-%d", curr)
			key, err := c.GetOrLoad(KeyMeta{id, created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, errors.New(fmt.Sprintf("loader should not be executed for id=%s", id))
			})
			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadExistingKey(b *testing.B) {
	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	c.mapLatestKeyMeta(testKey, KeyMeta{testKey, created})
	c.keys.Set(cacheKey(testKey, created), cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: internal.NewCryptoKeyForTest(created, false)},
		loadedAt: time.Now(),
	})

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := c.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, nil
			})
			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsWriteSameKey(b *testing.B) {
	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				return internal.NewCryptoKeyForTest(created, false), nil
			})
			assert.NoError(b, err)

			latest, _ := c.getLatestKeyMeta(testKey)
			latestKey := cacheKey(latest.ID, latest.Created)
			assert.Equal(b, created, c.keys.GetOrPanic(latestKey).key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsWriteUniqueKey(b *testing.B) {
	var (
		c = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		i int64
	)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.AddInt64(&i, 1) - 1
			_, err := c.GetOrLoadLatest(cacheKey(testKey, curr), func(_ KeyMeta) (*internal.CryptoKey, error) {
				return internal.NewCryptoKeyForTest(created, false), nil
			})
			assert.NoError(b, err)
		}
	})
	assert.NotNil(b, c.keys)

	expected := i
	if expected > DefaultKeyCacheMaxSize {
		expected = DefaultKeyCacheMaxSize
	}

	assert.Equal(b, expected, int64(c.keys.Len()))
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadStaleRevokedKey(b *testing.B) {
	ConfigureLogging()

	var (
		c       = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))
	cacheEntry := cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: key, refs: 1},
		loadedAt: time.Unix(created, 0),
	}

	assert.NoError(b, err)

	defer c.Close()

	meta := KeyMeta{ID: testKey, Created: created}
	c.mapLatestKeyMeta(testKey, meta)
	c.keys.Set(cacheKey(meta.ID, meta.Created), cacheEntry)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := c.GetOrLoadLatest(testKey, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				return internal.NewCryptoKey(secretFactory, time.Now().Unix(), true, []byte("testing"))
			})

			assert.NoError(b, err)
			assert.True(b, key.Revoked())
			assert.Greater(b, key.Created(), created)
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadRevokedKey(b *testing.B) {
	ConfigureLogging()

	var (
		c       = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		created = time.Now().Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, true, []byte("testing"))
	cacheEntry := cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: key, refs: 1},
		loadedAt: time.Unix(created, 0),
	}

	assert.NoError(b, err)

	defer c.Close()

	meta := KeyMeta{ID: testKey, Created: created}
	c.mapLatestKeyMeta(testKey, meta)
	c.keys.Set(cacheKey(meta.ID, meta.Created), cacheEntry)

	count := atomic.Int64{}
	reloadCount := atomic.Int64{}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			count.Add(1)

			key, err := c.GetOrLoadLatest(testKey, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				reloadCount.Add(1)

				return internal.NewCryptoKey(secretFactory, time.Now().Unix(), false, []byte("testing"))
			})

			assert.NoError(b, err)
			assert.False(b, key.Revoked())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadUniqueKeys(b *testing.B) {
	ConfigureLogging()

	c := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	for i := 0; i < b.N && i < DefaultKeyCacheMaxSize; i++ {
		keyID := fmt.Sprintf(testKey+"-%d", i)
		meta := KeyMeta{ID: keyID, Created: created}
		c.mapLatestKeyMeta(keyID, meta)
		c.keys.Set(cacheKey(meta.ID, meta.Created), cacheEntry{
			key:      &cachedCryptoKey{CryptoKey: internal.NewCryptoKeyForTest(created, false)},
			loadedAt: time.Now(),
		})
	}

	i := atomic.Int64{}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := i.Add(1) - 1
			curr = curr % DefaultKeyCacheMaxSize

			keyID := fmt.Sprintf(testKey+"-%d", curr)

			key, err := c.GetOrLoadLatest(keyID, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, errors.New(fmt.Sprintf("loader should not be executed for id=%s", keyID))
			})
			if err != nil {
				b.Error(err)
			}

			assert.Equal(b, created, key.Created())

			key.Close()
		}
	})
}
