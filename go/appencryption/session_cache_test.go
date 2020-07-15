package appencryption_test

import (
	"strconv"
	"sync"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption"
)

type sessionBucket struct {
	sessions map[string]*appencryption.Session
	loader   func(string) (*appencryption.Session, error)
}

func (b *sessionBucket) len() int {
	return len(b.sessions)
}

func (b *sessionBucket) fillAndAssertCacheContents(t *testing.T, cache appencryption.SessionCache) {
	totalSessions := b.len()

	// let's run through the items a few times to make sure our stats-based count is working
	for i := 0; i < 3; i++ {
		for k := 0; k < totalSessions; k++ {
			key := strconv.Itoa(k)
			expected := b.sessions[key]
			actual, err := cache.Get(key)

			require.NoError(t, err)
			assert.Same(t, expected, actual)
		}
	}
}

func newSessionBucket(count int) *sessionBucket {
	sessions := make(map[string]*appencryption.Session, count)

	for i := 0; i < count; i++ {
		s, _ := newSessionWithMockEncryption()
		sessions[strconv.Itoa(i)] = s
	}

	loader := func(id string) (*appencryption.Session, error) {
		if s, ok := sessions[id]; ok {
			return s, nil
		}

		s, _ := newSessionWithMockEncryption()

		return s, nil
	}

	return &sessionBucket{
		sessions: sessions,
		loader:   loader,
	}
}

func newSessionWithMockEncryption(callbacks ...func(mock.Arguments)) (*appencryption.Session, *appencryption.MockEncryption) {
	s := new(appencryption.Session)
	m := new(appencryption.MockEncryption)
	call := m.On("Close").Return(nil)

	for i := range callbacks {
		call = call.Run(callbacks[i])
	}

	appencryption.SessionInjectEncryption(s, m)

	return s, m
}

func TestNewSessionCache(t *testing.T) {
	loader := func(id string) (*appencryption.Session, error) {
		return &appencryption.Session{}, nil
	}

	cache := appencryption.NewSessionCache(loader, appencryption.NewCryptoPolicy())
	defer cache.Close()

	require.NotNil(t, cache)
}

func TestSessionCacheGetUsesLoader(t *testing.T) {
	session, _ := newSessionWithMockEncryption()

	loader := func(id string) (*appencryption.Session, error) {
		return session, nil
	}

	cache := appencryption.NewSessionCache(loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	val, err := cache.Get("some-id")
	require.NoError(t, err)
	assert.Same(t, session, val)
}

func TestSessionCacheCount(t *testing.T) {
	totalSessions := 10
	b := newSessionBucket(totalSessions)

	cache := appencryption.NewSessionCache(b.loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	assert.Eventually(t, func() bool { return totalSessions == cache.Count() }, time.Second, time.Millisecond*10)
}

func TestSessionCacheMaxCount(t *testing.T) {
	totalSessions := 20
	maxSessions := 10
	b := newSessionBucket(totalSessions)

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheSize = maxSessions

	cache := appencryption.NewSessionCache(b.loader, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	assert.Eventually(t, func() bool { return maxSessions == cache.Count() }, time.Second, time.Millisecond*10)
}

func TestSessionCacheTTL(t *testing.T) {
	ttl := time.Millisecond * 100

	// can't use more than 16 sessions here as that is the max drain for this cache implementation
	totalSessions := 16
	b := newSessionBucket(totalSessions)

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheTTL = ttl

	cache := appencryption.NewSessionCache(b.loader, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	// ensure the ttl has elapsed
	time.Sleep(ttl + time.Millisecond*50)

	// mango cache implementation only reaps expired entries following a write, so we'll write a new
	// cache entry and ensure it's the only one left
	_, _ = cache.Get("99") // IDs 0-15 were created above

	assert.Eventually(t, func() bool { return cache.Count() == 1 }, time.Second*10, time.Millisecond*10)
}

func TestSharedSessionCloseOnCacheClose(t *testing.T) {
	var (
		mu     sync.Mutex
		closed bool
	)

	s, _ := newSessionWithMockEncryption(func(_ mock.Arguments) {
		mu.Lock()
		defer mu.Unlock()

		closed = true
	})

	loader := func(id string) (*appencryption.Session, error) {
		return s, nil
	}

	cache := appencryption.NewSessionCache(loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	retrieved, _ := cache.Get("my-item")
	assert.Same(t, s, retrieved)

	// TODO: remove once we move from goburrow/cache to dgraph-io/ristretto
	time.Sleep(time.Millisecond * 50)

	cache.Close()

	assert.Eventually(t, func() bool {
		mu.Lock()
		defer mu.Unlock()

		return closed
	}, time.Second, time.Millisecond*10)
}

func TestSharedSessionCloseOnEviction(t *testing.T) {
	var (
		mu     sync.Mutex
		closed bool
	)

	s1, _ := newSessionWithMockEncryption(func(_ mock.Arguments) {
		mu.Lock()
		defer mu.Unlock()

		closed = true
	})

	s2, m2 := newSessionWithMockEncryption()
	sessions := map[string]*appencryption.Session{
		"s1": s1,
		"s2": s2,
	}

	loader := func(id string) (*appencryption.Session, error) {
		return sessions[id], nil
	}

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheSize = 1

	cache := appencryption.NewSessionCache(loader, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	cache.Get("s1")
	cache.Get("s2")

	assert.Eventually(t, func() bool {
		mu.Lock()
		defer mu.Unlock()

		return closed
	}, time.Second, time.Millisecond*10)

	m2.AssertNotCalled(t, "Close")
}

func TestSharedSessionClose(t *testing.T) {
	var (
		mu     sync.Mutex
		closed bool
	)

	s, _ := newSessionWithMockEncryption(func(_ mock.Arguments) {
		mu.Lock()
		defer mu.Unlock()

		closed = true
	})

	loader := func(id string) (*appencryption.Session, error) {
		return s, nil
	}

	cache := appencryption.NewSessionCache(loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	retrieved, _ := cache.Get("my-item")
	assert.Same(t, s, retrieved)

	retrieved.Close()

	assert.Eventually(t, func() bool {
		mu.Lock()
		defer mu.Unlock()

		return closed
	}, time.Second, time.Millisecond*10)
}

func TestSharedSessionNotClosedWhenInUse(t *testing.T) {
	s, m := newSessionWithMockEncryption()
	loader := func(id string) (*appencryption.Session, error) {
		return s, nil
	}

	cache := appencryption.NewSessionCache(loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	retrieved, _ := cache.Get("my-item")
	require.Same(t, s, retrieved)

	// TODO: remove once we move from goburrow/cache to dgraph-io/ristretto
	time.Sleep(time.Millisecond * 50)

	retrieved2, _ := cache.Get("my-item")
	defer retrieved2.Close()

	require.Same(t, retrieved, retrieved2)

	retrieved.Close()

	m.AssertNotCalled(t, "Close")

	assert.Equal(t, 1, cache.Count())
}
