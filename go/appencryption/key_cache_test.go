package appencryption

import (
	"fmt"
	"math/rand"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

const (
	testKey = "TestKey"
)

type CacheTestSuite struct {
	suite.Suite
	policy   *CryptoPolicy
	keyCache *keyCache
	created  int64
}

func (suite *CacheTestSuite) SetupTest() {
	suite.policy = NewCryptoPolicy()
	suite.keyCache = newKeyCache(CacheTypeIntermediateKeys, suite.policy)
	suite.created = time.Now().Unix()
}

func (suite *CacheTestSuite) TearDownTest() {
	suite.keyCache.Close()
}

func (suite *CacheTestSuite) Test_CacheKey() {
	key := cacheKey(testKey, suite.created)

	assert.Contains(suite.T(), key, testKey)
	assert.Contains(suite.T(), key, fmt.Sprintf("%d", suite.created))
}

func (suite *CacheTestSuite) Test_NewKeyCache() {
	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	assert.NotNil(suite.T(), cache)
	assert.IsType(suite.T(), new(keyCache), cache)
	assert.NotNil(suite.T(), cache.keys)
	assert.NotNil(suite.T(), cache.policy)
	assert.Equal(suite.T(), DefaultKeyCacheMaxSize, cache.keys.Capacity())
}

func (suite *CacheTestSuite) Test_IsReloadRequired_WithIntervalNotElapsed() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			loadedAt: time.Now(),
			key:      &cachedCryptoKey{CryptoKey: key},
		}

		defer key.Close()

		assert.False(suite.T(), isReloadRequired(entry, time.Hour))
	}
}

func (suite *CacheTestSuite) Test_IsReloadRequired_WithIntervalElapsed() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			loadedAt: time.Now().Add(-2 * time.Hour),
			key:      &cachedCryptoKey{CryptoKey: key},
		}

		defer key.Close()

		assert.True(suite.T(), isReloadRequired(entry, time.Hour))
	}
}

func (suite *CacheTestSuite) Test_IsReloadRequired_WithRevoked() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			// Note this loadedAt would normally require reload
			loadedAt: time.Now().Add(-2 * time.Hour),
			key:      &cachedCryptoKey{CryptoKey: key},
		}

		defer key.Close()

		assert.False(suite.T(), isReloadRequired(entry, time.Hour))
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyNoReloadRequired() {
	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))

		return cryptoKey, err
	})
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return nil, errors.New("should not be called")
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithEmptyCache() {
	meta := KeyMeta{ID: testKey, Created: suite.created}
	key, err := suite.keyCache.GetOrLoad(meta, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())

	latestKey, _ := suite.keyCache.getLatestKeyMeta(testKey)
	assert.Equal(suite.T(), latestKey, meta)
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_DoesNotSetKeyOnError() {
	key, err := suite.keyCache.GetOrLoad(KeyMeta{}, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return new(internal.CryptoKey), errors.New("error")
	})

	if assert.Error(suite.T(), err) {
		assert.Nil(suite.T(), key)
		assert.Zero(suite.T(), suite.keyCache.keys.Len())
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithOldCachedKeyLoadNewerUpdatesLatest() {
	olderCreated := time.Now().Add(-(time.Hour * 24)).Unix()

	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, olderCreated}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, olderCreated, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	})
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, func(_ KeyMeta) (*internal.CryptoKey, error) {
		cryptoKey, err2 := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("newerblah"))
		if err2 != nil {
			return nil, err2
		}
		return cryptoKey, nil
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())

	latestKey, _ := suite.keyCache.getLatestKeyMeta(testKey)
	assert.Equal(suite.T(), latestKey, KeyMeta{ID: testKey, Created: key.Created()})

	assert.Equal(suite.T(), suite.created, suite.keyCache.keys.GetOrPanic(cacheKey(testKey, suite.created)).key.Created())
	assert.Equal(suite.T(), olderCreated, suite.keyCache.keys.GetOrPanic(cacheKey(testKey, olderCreated)).key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyReloadRequiredAndNowRevoked() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      &cachedCryptoKey{CryptoKey: key},
			loadedAt: time.Now().Add(-2 * suite.policy.RevokeCheckInterval),
		}

		suite.keyCache.keys.Set(cacheKey(testKey, suite.created), entry)
		suite.keyCache.keys.Set(cacheKey(testKey, 0), entry)

		revokedKey, e := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
		if assert.NoError(suite.T(), e) {
			key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, func(_ KeyMeta) (*internal.CryptoKey, error) {
				return revokedKey, nil
			})

			assert.NoError(suite.T(), err)
			assert.NotNil(suite.T(), key)
			assert.Equal(suite.T(), suite.created, key.Created())
			assert.True(suite.T(), key.Revoked())
			assert.True(suite.T(), suite.keyCache.keys.GetOrPanic(cacheKey(testKey, 0)).key.Revoked())
			// Verify we closed the new one we loaded and kept the cached one open
			assert.True(suite.T(), revokedKey.IsClosed())
			assert.False(suite.T(), suite.keyCache.keys.GetOrPanic(cacheKey(testKey, suite.created)).key.IsClosed())
		}
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyReloadRequiredButNotRevoked() {
	var created = time.Now().Add(-2 * suite.policy.RevokeCheckInterval).Unix()
	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))

	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      &cachedCryptoKey{CryptoKey: key},
			loadedAt: time.Unix(created, 0),
		}

		suite.keyCache.keys.Set(cacheKey(testKey, created), entry)
		suite.keyCache.keys.Set(cacheKey(testKey, 0), entry)

		reloadedKey, e := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
		assert.NoError(suite.T(), e)

		key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (*internal.CryptoKey, error) {
			return reloadedKey, nil
		})

		assert.NoError(suite.T(), err)
		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), created, key.Created())
		assert.Greater(suite.T(), suite.keyCache.keys.GetOrPanic(cacheKey(testKey, created)).loadedAt.Unix(), created)

		// Verify we closed the new one we loaded and kept the cached one open
		assert.True(suite.T(), reloadedKey.IsClosed())
		assert.False(suite.T(), suite.keyCache.keys.GetOrPanic(cacheKey(testKey, created)).key.IsClosed())
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyNoReloadRequired() {
	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	})
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return nil, errors.New("should not be called")
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithEmptyCache() {
	key, err := suite.keyCache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())

	latestKey, _ := suite.keyCache.getLatestKeyMeta(testKey)
	assert.Equal(suite.T(), latestKey, KeyMeta{ID: testKey, Created: suite.created})
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_DoesNotSetKeyOnError() {
	key, err := suite.keyCache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return new(internal.CryptoKey), errors.New("error")
	})

	if assert.Error(suite.T(), err) {
		assert.Nil(suite.T(), key)
		assert.Zero(suite.T(), suite.keyCache.keys.Len())
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyReloadRequiredAndNowRevoked() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	suite.Require().NoError(err)

	entry := newCacheEntry(key)
	entry.loadedAt = time.Now().Add(-2 * suite.policy.RevokeCheckInterval)

	suite.keyCache.mapLatestKeyMeta(testKey, KeyMeta{ID: testKey, Created: suite.created})
	suite.keyCache.keys.Set(cacheKey(testKey, suite.created), entry)

	revokedKey, e := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
	suite.Require().NoError(e)

	first := true
	calls := 0

	// Because the entry's loadedAt is older than the revoke check interval, the key should be treated as "stale"
	// which should trigger the following:
	// 1. A cache miss is recorded because the key is no longer "fresh"
	// 2. The key is loaded via the loader function below, which returns a revoked key on the first call
	// 3. The cache, having received a revoked key, increments the reloaded count
	// 4. The key is reloaded via the loader function, which returns a new key on subsequent calls
	latest, err := suite.keyCache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		calls++

		if first {
			first = false
			return revokedKey, nil
		}

		return internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	})

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), latest)
	assert.Equal(suite.T(), suite.created, latest.Created())
	assert.Equal(suite.T(), 2, calls)
	assert.False(suite.T(), latest.Revoked())
	// Verify we closed the new one we loaded and kept the cached one open
	assert.True(suite.T(), revokedKey.IsClosed())
	assert.False(suite.T(), suite.keyCache.keys.GetOrPanic(cacheKey(testKey, suite.created)).key.IsClosed())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyAndInvalidKey() {
	orig, err := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
	require.NoError(suite.T(), err)

	entry := cacheEntry{
		key:      &cachedCryptoKey{CryptoKey: orig},
		loadedAt: time.Now(),
	}

	suite.keyCache.mapLatestKeyMeta(testKey, KeyMeta{ID: testKey, Created: suite.created})
	suite.keyCache.keys.Set(cacheKey(testKey, suite.created), entry)

	newerCreated := time.Now().Add(1 * time.Second).Unix()
	require.Greater(suite.T(), newerCreated, suite.created)

	loader := func(_ KeyMeta) (*internal.CryptoKey, error) {
		reloadedKey, e := internal.NewCryptoKey(secretFactory, newerCreated, false, []byte("blah"))
		assert.NoError(suite.T(), e)

		return reloadedKey, e
	}

	key, err := suite.keyCache.GetOrLoadLatest(testKey, loader)

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), newerCreated, key.Created())

	// the reloaded key is not revoked
	assert.False(suite.T(), key.Revoked())

	// cached key is still revoked
	cached := suite.keyCache.keys.GetOrPanic(cacheKey(testKey, suite.created))
	assert.True(suite.T(), cached.key.Revoked(), fmt.Sprintf("%+v - created: %d", cached.key, cached.key.Created()))
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyAndValidKey() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      &cachedCryptoKey{CryptoKey: key},
			loadedAt: time.Now(),
		}

		suite.keyCache.keys.Set(cacheKey(testKey, suite.created), entry)

		key, err := suite.keyCache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
			return key, nil
		})

		assert.NoError(suite.T(), err)
		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), suite.created, key.Created())
		assert.False(suite.T(), key.Revoked())
	}
}

func (suite *CacheTestSuite) TestKeyCache_Close() {
	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	key, err := cache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}

		return cryptoKey, nil
	})
	assert.NoError(suite.T(), err)

	key.Close()
	assert.False(suite.T(), key.IsClosed(), "key should not be closed yet, as it is still in the cache")

	err = cache.Close()

	assert.NoError(suite.T(), err)
	assert.True(suite.T(), key.IsClosed())
}

func (suite *CacheTestSuite) TestKeyCache_Close_CacheThenKey() {
	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	key, err := cache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}

		return cryptoKey, nil
	})
	assert.NoError(suite.T(), err)

	err = cache.Close()
	assert.NoError(suite.T(), err)
	assert.False(suite.T(), key.IsClosed(), "key should not be closed yet, key reference still exists")

	key.Close()
	assert.True(suite.T(), key.IsClosed())
}

func (suite *CacheTestSuite) TestKeyCache_Close_MultipleCallsNoError() {
	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())

	key, err := cache.GetOrLoadLatest(testKey, func(_ KeyMeta) (*internal.CryptoKey, error) {
		return internal.NewCryptoKey(secretFactory, time.Now().Unix(), false, []byte("blah"))
	})
	assert.NoError(suite.T(), err)

	key.Close()

	err = cache.Close()

	assert.NoError(suite.T(), err)
	assert.True(suite.T(), key.IsClosed())

	err = cache.Close()
	assert.NoError(suite.T(), err)
}

func (suite *CacheTestSuite) TestKeyCache_String() {
	cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
	defer cache.Close()

	assert.Contains(suite.T(), cache.String(), "keyCache(")
}

func (suite *CacheTestSuite) TestNeverCache_GetOrLoad() {
	var cache neverCache
	key, err := cache.GetOrLoad(KeyMeta{testKey, created}, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
		return internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
	})

	if assert.NoError(suite.T(), err) {
		// neverCache can't close keys we create
		defer key.Close()

		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), created, key.Created())
	}
}

func (suite *CacheTestSuite) TestNeverCache_GetOrLoadLatest() {
	var cache neverCache
	key, err := cache.GetOrLoadLatest(testKey, func(_ KeyMeta) (key *internal.CryptoKey, e error) {
		return internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
	})

	if assert.NoError(suite.T(), err) {
		// neverCache can't close keys we create
		defer key.Close()

		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), created, key.Created())
	}
}

func (suite *CacheTestSuite) TestNeverCache_Close() {
	var c neverCache

	err := c.Close()

	assert.NoError(suite.T(), err)
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_Concurrent_100() {
	if testing.Short() {
		suite.T().Skip("too slow for testing.Short")
	}

	var (
		cache   = newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
		i       = 0
		wg      sync.WaitGroup
		counter int32
	)

	loadFunc := func(_ KeyMeta) (*internal.CryptoKey, error) {
		<-time.After(time.Millisecond * time.Duration(rand.Intn(30)))
		atomic.AddInt32(&counter, 1)

		return new(internal.CryptoKey), nil
	}

	meta := KeyMeta{ID: "testing", Created: time.Now().Unix()}

	_, err := cache.GetOrLoad(meta, loadFunc)
	if err != nil {
		suite.T().Error(err)
	}

	for ; i < 100; i++ {
		wg.Add(1)

		go func() {
			defer wg.Done()

			key, err := cache.GetOrLoad(meta, loadFunc)
			if key == nil {
				suite.T().Error("key == nil")
			}

			if err != nil {
				suite.T().Error(err)
			}
		}()
	}

	wg.Wait()

	assert.Equal(suite.T(), int32(1), counter)
	assert.Equal(suite.T(), 1, cache.keys.Len())

	// metrics := cache.GetMetrics()
	// assert.Equal(suite.T(), int64(1), metrics.MissCount)
	// assert.Equal(suite.T(), int64(100), metrics.HitCount)
}

func TestCacheTestSuite(t *testing.T) {
	suite.Run(t, new(CacheTestSuite))
}
