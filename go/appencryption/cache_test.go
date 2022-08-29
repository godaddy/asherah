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
	"github.com/stretchr/testify/mock"
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
	suite.keyCache = newKeyCache(suite.policy)
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
	cache := newKeyCache(NewCryptoPolicy())
	defer cache.Close()

	assert.NotNil(suite.T(), cache)
	assert.IsType(suite.T(), new(keyCache), cache)
	assert.NotNil(suite.T(), cache.keys)
	assert.NotNil(suite.T(), cache.policy)
}

func (suite *CacheTestSuite) Test_IsReloadRequired_WithIntervalNotElapsed() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			loadedAt: time.Now(),
			key:      key,
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
			key:      key,
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
			key:      key,
		}

		defer key.Close()

		assert.False(suite.T(), isReloadRequired(entry, time.Hour))
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyNoReloadRequired() {
	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))

		return cryptoKey, err
	}))
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return nil, errors.New("should not be called")
	}))

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithEmptyCache() {
	key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
	assert.Equal(suite.T(), suite.created, suite.keyCache.keys[cacheKey(testKey, 0)].key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_DoesNotSetKeyOnError() {
	key, err := suite.keyCache.GetOrLoad(KeyMeta{}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return new(internal.CryptoKey), errors.New("error")
	}))

	if assert.Error(suite.T(), err) {
		assert.Nil(suite.T(), key)
		assert.Empty(suite.T(), suite.keyCache.keys)
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithOldCachedKeyLoadNewerUpdatesLatest() {
	olderCreated := time.Now().Add(-(time.Hour * 24)).Unix()

	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, olderCreated}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, olderCreated, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		cryptoKey, err2 := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("newerblah"))
		if err2 != nil {
			return nil, err2
		}
		return cryptoKey, nil
	}))

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
	assert.Equal(suite.T(), suite.created, suite.keyCache.keys[cacheKey(testKey, 0)].key.Created())
	assert.Equal(suite.T(), suite.created, suite.keyCache.keys[cacheKey(testKey, suite.created)].key.Created())
	assert.Equal(suite.T(), olderCreated, suite.keyCache.keys[cacheKey(testKey, olderCreated)].key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyReloadRequiredAndNowRevoked() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      key,
			loadedAt: time.Now().Add(-2 * suite.policy.RevokeCheckInterval),
		}

		suite.keyCache.keys[cacheKey(testKey, suite.created)] = entry
		suite.keyCache.keys[cacheKey(testKey, 0)] = entry

		revokedKey, e := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
		if assert.NoError(suite.T(), e) {
			key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
				return revokedKey, nil
			}))

			assert.NoError(suite.T(), err)
			assert.NotNil(suite.T(), key)
			assert.Equal(suite.T(), suite.created, key.Created())
			assert.True(suite.T(), key.Revoked())
			assert.True(suite.T(), suite.keyCache.keys[cacheKey(testKey, 0)].key.Revoked())
			// Verify we closed the new one we loaded and kept the cached one open
			assert.True(suite.T(), revokedKey.IsClosed())
			assert.False(suite.T(), suite.keyCache.keys[cacheKey(testKey, suite.created)].key.IsClosed())
		}
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoad_WithCachedKeyReloadRequiredButNotRevoked() {
	var created = time.Now().Add(-2 * suite.policy.RevokeCheckInterval).Unix()
	key, err := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))

	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      key,
			loadedAt: time.Unix(created, 0),
		}

		suite.keyCache.keys[cacheKey(testKey, created)] = entry
		suite.keyCache.keys[cacheKey(testKey, 0)] = entry

		reloadedKey, e := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
		assert.NoError(suite.T(), e)

		key, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (*internal.CryptoKey, error) {
			return reloadedKey, nil
		}))

		assert.NoError(suite.T(), err)
		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), created, key.Created())
		assert.Greater(suite.T(), suite.keyCache.keys[cacheKey(testKey, created)].loadedAt.Unix(), created)

		// Verify we closed the new one we loaded and kept the cached one open
		assert.True(suite.T(), reloadedKey.IsClosed())
		assert.False(suite.T(), suite.keyCache.keys[cacheKey(testKey, created)].key.IsClosed())
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyNoReloadRequired() {
	_, err := suite.keyCache.GetOrLoad(KeyMeta{testKey, suite.created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))
	assert.NoError(suite.T(), err)

	key, err := suite.keyCache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return nil, errors.New("should not be called")
	}))

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithEmptyCache() {
	key, err := suite.keyCache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), suite.created, key.Created())
	assert.Equal(suite.T(), suite.created, suite.keyCache.keys[cacheKey(testKey, 0)].key.Created())
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_DoesNotSetKeyOnError() {
	key, err := suite.keyCache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return new(internal.CryptoKey), errors.New("error")
	}))

	if assert.Error(suite.T(), err) {
		assert.Nil(suite.T(), key)
		assert.Empty(suite.T(), suite.keyCache.keys)
	}
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_WithCachedKeyReloadRequiredAndNowRevoked() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      key,
			loadedAt: time.Now().Add(-2 * suite.policy.RevokeCheckInterval),
		}

		suite.keyCache.keys[cacheKey(testKey, suite.created)] = entry
		suite.keyCache.keys[cacheKey(testKey, 0)] = entry

		revokedKey, e := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
		if assert.NoError(suite.T(), e) {
			key, err := suite.keyCache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
				return revokedKey, nil
			}))

			assert.NoError(suite.T(), err)
			assert.NotNil(suite.T(), key)
			assert.Equal(suite.T(), suite.created, key.Created())
			assert.True(suite.T(), key.Revoked())
			assert.True(suite.T(), suite.keyCache.keys[cacheKey(testKey, 0)].key.Revoked())
			// Verify we closed the new one we loaded and kept the cached one open
			assert.True(suite.T(), revokedKey.IsClosed())
			assert.False(suite.T(), suite.keyCache.keys[cacheKey(testKey, suite.created)].key.IsClosed())
		}
	}
}

type mockKeyReloader struct {
	mock.Mock

	loader keyLoaderFunc
}

func (r *mockKeyReloader) Load() (*internal.CryptoKey, error) {
	args := r.Called()

	if r.loader != nil {
		return r.loader()
	}

	return args.Get(0).(*internal.CryptoKey), args.Error(1)
}

func (r *mockKeyReloader) IsInvalid(key *internal.CryptoKey) bool {
	args := r.Called(key.Created())
	return args.Bool(0)
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_KeyReloader_WithCachedKeyAndInvalidKey() {
	orig, err := internal.NewCryptoKey(secretFactory, suite.created, true, []byte("blah"))
	require.NoError(suite.T(), err)

	entry := cacheEntry{
		key:      orig,
		loadedAt: time.Now(),
	}

	suite.keyCache.keys[cacheKey(testKey, suite.created)] = entry
	suite.keyCache.keys[cacheKey(testKey, 0)] = entry

	newerCreated := time.Now().Add(1 * time.Second).Unix()
	require.Greater(suite.T(), newerCreated, suite.created)

	reloader := &mockKeyReloader{
		loader: keyLoaderFunc(func() (*internal.CryptoKey, error) {
			reloadedKey, e := internal.NewCryptoKey(secretFactory, newerCreated, false, []byte("blah"))
			assert.NoError(suite.T(), e)

			return reloadedKey, e
		}),
	}
	reloader.On("IsInvalid", orig.Created()).Return(true)
	reloader.On("Load").Return().Once()

	key, err := suite.keyCache.GetOrLoadLatest(testKey, reloader)

	reloader.AssertExpectations(suite.T())

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), key)
	assert.Equal(suite.T(), newerCreated, key.Created())

	// the reloaded key is not revoked
	assert.False(suite.T(), key.Revoked())

	// cached key is still revoked
	cached := suite.keyCache.keys[cacheKey(testKey, suite.created)]
	assert.True(suite.T(), cached.key.Revoked(), fmt.Sprintf("%+v - created: %d", cached.key, cached.key.Created()))
}

func (suite *CacheTestSuite) TestKeyCache_GetOrLoadLatest_KeyReloader_WithCachedKeyAndValidKey() {
	key, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
	if assert.NoError(suite.T(), err) {
		entry := cacheEntry{
			key:      key,
			loadedAt: time.Now(),
		}

		suite.keyCache.keys[cacheKey(testKey, suite.created)] = entry
		suite.keyCache.keys[cacheKey(testKey, 0)] = entry

		reloader := new(mockKeyReloader)
		reloader.On("IsInvalid", key.Created()).Return(false)

		key, err := suite.keyCache.GetOrLoadLatest(testKey, reloader)

		assert.NoError(suite.T(), err)
		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), suite.created, key.Created())
		assert.False(suite.T(), suite.keyCache.keys[cacheKey(testKey, 0)].key.Revoked())

		reloader.AssertNotCalled(suite.T(), "Load", mock.Anything)
		reloader.AssertExpectations(suite.T())
	}
}

func (suite *CacheTestSuite) TestKeyCache_Close() {
	cache := newKeyCache(NewCryptoPolicy())

	key, err := cache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, suite.created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))
	assert.NoError(suite.T(), err)

	err = cache.Close()

	assert.NoError(suite.T(), err)
	assert.True(suite.T(), key.IsClosed())
	assert.True(suite.T(), cache.keys[cacheKey(testKey, suite.created)].key.IsClosed())
	assert.True(suite.T(), cache.keys[cacheKey(testKey, 0)].key.IsClosed())
}

func (suite *CacheTestSuite) TestKeyCache_Close_MultipleCallsNoError() {
	cache := newKeyCache(NewCryptoPolicy())

	key, err := cache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (*internal.CryptoKey, error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, time.Now().Unix(), false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))
	assert.NoError(suite.T(), err)

	err = cache.Close()

	assert.NoError(suite.T(), err)
	assert.True(suite.T(), key.IsClosed())

	err = cache.Close()

	assert.NoError(suite.T(), err)
}

func (suite *CacheTestSuite) TestKeyCache_String() {
	cache := newKeyCache(NewCryptoPolicy())
	defer cache.Close()

	assert.Contains(suite.T(), cache.String(), "keyCache(")
}

func (suite *CacheTestSuite) TestNeverCache_GetOrLoad() {
	var cache neverCache
	key, err := cache.GetOrLoad(KeyMeta{testKey, created}, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))

	if assert.NoError(suite.T(), err) {
		// neverCache can't close keys we create
		defer key.Close()

		assert.NotNil(suite.T(), key)
		assert.Equal(suite.T(), created, key.Created())
	}
}

func (suite *CacheTestSuite) TestNeverCache_GetOrLoadLatest() {
	var cache neverCache
	key, err := cache.GetOrLoadLatest(testKey, keyLoaderFunc(func() (key *internal.CryptoKey, e error) {
		cryptoKey, err := internal.NewCryptoKey(secretFactory, created, false, []byte("blah"))
		if err != nil {
			return nil, err
		}
		return cryptoKey, nil
	}))

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

func (suite *CacheTestSuite) TestSharedKeyCache_GetOrLoad() {
	if testing.Short() {
		suite.T().Skip("too slow for testing.Short")
	}

	var (
		cache   = newKeyCache(NewCryptoPolicy())
		i       = 0
		wg      sync.WaitGroup
		counter int32
	)

	loadFunc := keyLoaderFunc(func() (*internal.CryptoKey, error) {
		<-time.After(time.Nanosecond * time.Duration(rand.Intn(30)))
		atomic.AddInt32(&counter, 1)

		return new(internal.CryptoKey), nil
	})

	meta := KeyMeta{ID: "testing", Created: time.Now().Unix()}

	for ; i < 100; i++ {
		wg.Add(1)

		go func() {
			defer wg.Done()

			key, err := cache.GetOrLoad(meta, loadFunc)
			if key == nil {
				suite.T().Error("key == nil")
				suite.T().Fail()
			}

			if err != nil {
				suite.T().Error(err)
				suite.T().Fail()
			}
		}()
	}

	wg.Wait()

	// This seems to be causing intermittent issues with go2xunit parsing
	//d := time.Since(startTime)
	//
	//fmt.Printf("Finished %d loops in: %s (%f/s)", i, d, float64(i)/d.Seconds())

	assert.Equal(suite.T(), int32(1), counter)
}

func TestCacheTestSuite(t *testing.T) {
	suite.Run(t, new(CacheTestSuite))
}
