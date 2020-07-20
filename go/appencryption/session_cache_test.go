package appencryption_test

import (
	"fmt"
	"strconv"
	"sync"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption"
)

type closeSpy struct {
	isClosed bool
	mu       sync.Mutex
}

func (s *closeSpy) IsClosed() bool {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.isClosed
}

func (s *closeSpy) SetClosed() {
	s.mu.Lock()
	s.isClosed = true
	s.mu.Unlock()
}

type sessionBucket struct {
	closeSpies map[*appencryption.Session]*closeSpy
	mu         sync.Mutex
}

func (b *sessionBucket) load(_ string) (*appencryption.Session, error) {
	b.mu.Lock()
	defer b.mu.Unlock()

	return b.newSession(), nil
}

func (b *sessionBucket) newSession() *appencryption.Session {
	spy := &closeSpy{}

	s := newSessionWithMockEncryption(func(_ mock.Arguments) {
		b.mu.Lock()
		defer b.mu.Unlock()

		spy.SetClosed()
	})
	b.closeSpies[s] = spy

	return s
}

func (b *sessionBucket) IsClosed(s *appencryption.Session) bool {
	b.mu.Lock()
	defer b.mu.Unlock()

	if spy, ok := b.closeSpies[s]; ok {
		return spy.IsClosed()
	}

	return false
}

func newSessionBucket() *sessionBucket {
	return &sessionBucket{
		closeSpies: make(map[*appencryption.Session]*closeSpy),
	}
}

func newSessionWithMockEncryption(callbacks ...func(mock.Arguments)) *appencryption.Session {
	s := new(appencryption.Session)
	m := new(appencryption.MockEncryption)
	call := m.On("Close").Return(nil)

	for i := range callbacks {
		call = call.Run(callbacks[i])
	}

	appencryption.SessionInjectEncryption(s, m)

	return s
}

func TestNewSessionCache(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		loader := func(id string) (*appencryption.Session, error) {
			return &appencryption.Session{}, nil
		}

		cache := appencryption.NewSessionCache(loader, policy)
		defer cache.Close()

		require.NotNil(t, cache)
	})
}

func withEachEngine(t *testing.T, testFunc func(*testing.T, *appencryption.CryptoPolicy)) {
	var engines = [...]string{
		"mango",
		"ristretto",
	}

	for i := range engines {
		engine := engines[i]
		policy := appencryption.NewCryptoPolicy(
			appencryption.WithSessionCacheEngine(engine),
		)

		t.Run(fmt.Sprintf("WithEngine %s", engine), func(t *testing.T) {
			testFunc(t, policy)
		})
	}
}

func TestNewSessionCachePanicsWithUnknownEngine(t *testing.T) {
	loader := func(id string) (*appencryption.Session, error) {
		return &appencryption.Session{}, nil
	}

	engine := "bogus"
	policy := appencryption.NewCryptoPolicy(
		appencryption.WithSessionCacheEngine(engine),
	)

	assert.PanicsWithValue(t, "invalid session cache engine: "+engine, func() {
		appencryption.NewSessionCache(loader, policy)
	})
}

func TestSessionCacheGetUsesLoader(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		session := newSessionWithMockEncryption()

		loader := func(id string) (*appencryption.Session, error) {
			return session, nil
		}

		cache := appencryption.NewSessionCache(loader, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		val, err := cache.Get("some-id")
		require.NoError(t, err)
		assert.Same(t, session, val)
	})
}

func TestSessionCacheGetReturnLoaderError(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		loader := func(id string) (*appencryption.Session, error) {
			return nil, assert.AnError
		}

		cache := appencryption.NewSessionCache(loader, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		val, err := cache.Get("some-id")
		assert.Nil(t, val)
		assert.EqualError(t, err, assert.AnError.Error())
	})
}

func TestSessionCacheCount(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		totalSessions := 10
		b := newSessionBucket()

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		for i := 0; i < totalSessions; i++ {
			cache.Get(strconv.Itoa(i))
		}

		assert.Eventually(t, func() bool { return totalSessions == cache.Count() }, time.Second, time.Millisecond*10)
	})
}

func TestSessionCacheMaxCount(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		totalSessions := 20
		maxSessions := 10
		b := newSessionBucket()

		policy.SessionCacheSize = maxSessions

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		sessions := make([]*appencryption.Session, totalSessions)
		for i := 0; i < totalSessions; i++ {
			s, err := cache.Get(strconv.Itoa(i))

			require.NoError(t, err)
			sessions[i] = s

			s.Close()
		}

		// assert we have no more than the max
		assert.Eventually(t, func() bool { return maxSessions == cache.Count() }, time.Second, time.Millisecond*10)

		// assert the others have been closed
		assert.Eventually(t, func() bool {
			closed := 0
			for i := 0; i < totalSessions; i++ {
				s := sessions[i]
				isClosed := b.closeSpies[s].IsClosed()

				if isClosed {
					closed++
				}
			}
			return closed == totalSessions-maxSessions
		}, time.Second*10, time.Millisecond*100)
	})
}

func TestSessionCacheTTL(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		ttl := time.Millisecond * 100

		// can't use more than 16 sessions here as that is the max drain
		// for the mango cache implementation
		totalSessions := 16
		b := newSessionBucket()

		policy.SessionCacheTTL = ttl

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		for i := 0; i < totalSessions; i++ {
			cache.Get(strconv.Itoa(i))
		}

		// ensure the ttl has elapsed
		time.Sleep(ttl + time.Millisecond*50)

		expectedCount := 0

		if policy.SessionCacheEngine == "mango" {
			// mango cache implementation only reaps expired entries following a write, so we'll write a new
			// cache entry and ensure it's the only one left
			_, _ = cache.Get("99") // IDs 0-15 were created above
			expectedCount = 1
		}

		assert.Eventually(t, func() bool {
			return cache.Count() == expectedCount
		}, time.Second*10, time.Millisecond*10)
	})
}

func TestSharedSessionCloseOnCacheClose(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		b := newSessionBucket()

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		s, err := cache.Get("my-item")
		require.NoError(t, err)
		require.NotNil(t, s)

		s.Close()
		cache.Close()

		assert.Eventually(t, func() bool {
			return b.IsClosed(s)
		}, time.Second*10, time.Millisecond*100)
	})
}

func TestSharedSessionCloseOnEviction(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		b := newSessionBucket()

		policy.SessionCacheSize = 1

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		s1, err := cache.Get("0")
		require.NoError(t, err)
		require.NotNil(t, s1)
		assert.Eventually(t, func() bool {
			return cache.Count() == 1
		}, time.Second*10, time.Millisecond*100)
		s1.Close()

		s2, err := cache.Get("1")
		require.NoError(t, err)
		require.NotNil(t, s2)

		assert.Eventually(t, func() bool {
			return b.IsClosed(s1)
		}, time.Second*10, time.Millisecond*100)

		assert.False(t, b.IsClosed(s2))
	})
}

func TestSharedSessionClose(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		b := newSessionBucket()

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		for i := 0; i < 2; i++ {
			s1, err := cache.Get("my-item")
			require.NoError(t, err)
			require.NotNil(t, s1)
			s1.Close()

			assert.Eventually(t, func() bool {
				return cache.Count() == 1
			}, time.Second*10, time.Millisecond*100)

			time.Sleep(time.Millisecond * 200)

			// shared sessions arent' actually closed until evicted from the cache
			assert.False(t, b.IsClosed(s1))
		}
	})
}

func TestSharedSessionNotClosedWhenInUse(t *testing.T) {
	withEachEngine(t, func(t *testing.T, policy *appencryption.CryptoPolicy) {
		b := newSessionBucket()

		cache := appencryption.NewSessionCache(b.load, policy)
		require.NotNil(t, cache)

		defer cache.Close()

		s1, err := cache.Get("my-item")
		require.NoError(t, err)
		require.NotNil(t, s1)
		assert.Eventually(t, func() bool {
			return cache.Count() == 1
		}, time.Second*10, time.Millisecond*100)

		s2, err := cache.Get("my-item")
		assert.NoError(t, err)
		assert.Same(t, s1, s2)

		defer s2.Close()

		assert.Eventually(t, func() bool {
			return cache.Count() == 1 && !b.IsClosed(s1)
		}, time.Second*10, time.Millisecond*100)
	})
}
