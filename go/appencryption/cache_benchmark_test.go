package appencryption

import (
	"fmt"
	"sync/atomic"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

var (
	secretFactory = new(memguard.SecretFactory)
	created       = time.Now().Unix()
)

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsReadExistingKey(b *testing.B) {
	c := newKeyCache(NewCryptoPolicy())

	c.keys[cacheKey(testKey, created)] = cacheEntry{
		key:      internal.NewCryptoKeyForTest(created, false),
		loadedAt: time.Now(),
	}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := c.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, nil
			}))

			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsWriteSameKey(b *testing.B) {
	c := newKeyCache(NewCryptoPolicy())

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				return internal.NewCryptoKeyForTest(created, false), nil
			}))

			assert.NoError(b, err)
			assert.Equal(b, created, c.keys[cacheKey(testKey, 0)].key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsWriteUniqueKeys(b *testing.B) {
	var (
		c = newKeyCache(NewCryptoPolicy())
		i int64
	)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.AddInt64(&i, 1)
			_, err := c.GetOrLoad(KeyMeta{cacheKey(testKey, curr), created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				return internal.NewCryptoKeyForTest(created, false), nil
			}))
			assert.NoError(b, err)

			// ensure we have a "latest" entry for this key as well
			latest, err := c.GetOrLoadLatest(cacheKey(testKey, curr), keyLoaderFunc(func() (*internal.CryptoKey, error) {
				return nil, errors.New("loader should not be executed")
			}))
			assert.NoError(b, err)
			assert.NotNil(b, latest)
		}
	})
	assert.NotNil(b, c.keys)
	assert.Equal(b, i*2, int64(len(c.keys)))
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsReadRevokedKey(b *testing.B) {
	var (
		c       = newKeyCache(NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))

	assert.NoError(b, err)

	cacheEntry := cacheEntry{
		key:      key,
		loadedAt: time.Unix(created, 0),
	}

	defer c.Close()
	c.keys[cacheKey(testKey, created)] = cacheEntry

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				key, err2 := internal.NewCryptoKey(secretFactory, created, true, []byte("testing"))
				if err2 != nil {
					return nil, err2
				}

				return key, nil
			}))

			assert.NoError(b, err)
			assert.Equal(b, created, c.keys[cacheKey(testKey, 0)].key.Created())
			assert.True(b, c.keys[cacheKey(testKey, 0)].key.Revoked())
			assert.True(b, c.keys[cacheKey(testKey, created)].key.Revoked())
		}
	})
}

func BenchmarkKeyCache_GetOrLoad_MultipleThreadsRead_NeedReloadKey(b *testing.B) {
	var (
		c       = newKeyCache(NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))

	assert.NoError(b, err)

	cacheEntry := cacheEntry{
		key:      key,
		loadedAt: time.Unix(created, 0),
	}

	defer c.Close()
	c.keys[cacheKey(testKey, created)] = cacheEntry

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			k, err := c.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
				// Note: this function should only happen on first load (although could execute more than once currently), if it doesn't, then something is broken

				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)

				return internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))
			}))

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
	var (
		c = newKeyCache(NewCryptoPolicy())
		i int64
	)

	for ; i < int64(b.N); i++ {
		c.keys[cacheKey(fmt.Sprintf(testKey+"-%d", i), created)] = cacheEntry{
			key:      internal.NewCryptoKeyForTest(created, false),
			loadedAt: time.Now(),
		}
	}

	i = 0

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.LoadInt64(&i)
			key, err := c.GetOrLoad(KeyMeta{cacheKey(testKey, curr), created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, nil
			}))
			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())

			atomic.AddInt64(&i, 1)
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadExistingKey(b *testing.B) {
	c := newKeyCache(NewCryptoPolicy())

	c.keys[cacheKey(testKey, 0)] = cacheEntry{
		key:      internal.NewCryptoKeyForTest(created, false),
		loadedAt: time.Now(),
	}

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			key, err := c.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, nil
			}))
			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsWriteSameKey(b *testing.B) {
	c := newKeyCache(NewCryptoPolicy())

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)
				return internal.NewCryptoKeyForTest(created, false), nil
			}))
			assert.NoError(b, err)
			assert.Equal(b, created, c.keys[cacheKey(testKey, 0)].key.Created())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsWriteUniqueKey(b *testing.B) {
	var (
		c = newKeyCache(NewCryptoPolicy())
		i int64
	)

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.AddInt64(&i, 1)
			_, err := c.GetOrLoadLatest(cacheKey(testKey, curr), keyLoaderFunc(func() (*internal.CryptoKey, error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)

				return internal.NewCryptoKeyForTest(created, false), nil
			}))
			assert.NoError(b, err)

			// ensure we actually have a "latest" entry for this key in the cache
			latest, err := c.GetOrLoadLatest(cacheKey(testKey, curr), keyLoaderFunc(func() (*internal.CryptoKey, error) {
				return nil, errors.New("loader should not be executed")
			}))
			assert.NoError(b, err)
			assert.NotNil(b, latest)
		}
	})
	assert.NotNil(b, c.keys)
	assert.Equal(b, i*2, int64(len(c.keys)))
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadRevokedKey(b *testing.B) {
	var (
		c       = newKeyCache(NewCryptoPolicy())
		created = time.Now().Add(-(time.Minute * 100)).Unix()
	)

	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("testing"))
	cacheEntry := cacheEntry{
		key:      key,
		loadedAt: time.Unix(created, 0),
	}

	assert.NoError(b, err)

	defer c.Close()

	c.keys[cacheKey(testKey, 0)] = cacheEntry

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			_, err := c.GetOrLoadLatest(testKey, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// Add a delay to simulate time spent in performing a metastore read
				time.Sleep(5 * time.Millisecond)

				return internal.NewCryptoKey(secretFactory, created, true, []byte("testing"))
			}))

			assert.NoError(b, err)
			assert.Equal(b, created, c.keys[cacheKey(testKey, 0)].key.Created())
			assert.True(b, c.keys[cacheKey(testKey, 0)].key.Revoked())
		}
	})
}

func BenchmarkKeyCache_GetOrLoadLatest_MultipleThreadsReadUniqueKeys(b *testing.B) {
	var (
		c = newKeyCache(NewCryptoPolicy())
		i int64
	)

	for ; i < int64(b.N); i++ {
		c.keys[cacheKey(fmt.Sprintf(testKey+"-%d", i), 0)] = cacheEntry{
			key:      internal.NewCryptoKeyForTest(created, false),
			loadedAt: time.Now(),
		}
	}

	i = 0

	b.ResetTimer()
	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			curr := atomic.LoadInt64(&i)
			key, err := c.GetOrLoadLatest(fmt.Sprintf(testKey+"-%d", curr), keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
				// The passed function is irrelevant because we'll always find the value in the cache
				return nil, nil
			}))

			assert.NoError(b, err)
			assert.Equal(b, created, key.Created())

			atomic.AddInt64(&i, 1)
		}
	})
}
