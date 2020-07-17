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
	sessions   map[string]*appencryption.Session
	closeSpies map[string]*closeSpy
	mu         sync.Mutex
}

func (b *sessionBucket) newSession(id string) *appencryption.Session {
	b.mu.Lock()
	defer b.mu.Unlock()

	spy := &closeSpy{}
	b.closeSpies[id] = spy

	s, _ := newSessionWithMockEncryption(func(_ mock.Arguments) {
		spy.SetClosed()
	})

	b.sessions[id] = s

	return s
}

func (b *sessionBucket) load(id string) (*appencryption.Session, error) {
	if s, ok := b.get(id); ok {
		return s, nil
	}

	return b.newSession(id), nil
}

func (b *sessionBucket) get(id string) (s *appencryption.Session, ok bool) {
	b.mu.Lock()
	defer b.mu.Unlock()

	s, ok = b.sessions[id]

	return
}

func (b *sessionBucket) len() int {
	b.mu.Lock()
	defer b.mu.Unlock()

	return len(b.sessions)
}

func (b *sessionBucket) IsClosed(id string) bool {
	b.mu.Lock()
	defer b.mu.Unlock()

	if spy, ok := b.closeSpies[id]; ok {
		return spy.IsClosed()
	}

	return false
}

func (b *sessionBucket) fillAndAssertCacheContents(t *testing.T, cache appencryption.SessionCache) {
	totalSessions := b.len()

	// run through the items a few times to make sure our metrics-based count is working
	for i := 0; i < 3; i++ {
		for k := 0; k < totalSessions; k++ {
			key := strconv.Itoa(k)
			expected, _ := b.get(key)
			actual, err := cache.Get(key)

			require.NoError(t, err)
			assert.Same(t, expected, actual)
			actual.Close()
		}
	}
}

func newSessionBucket(count int) *sessionBucket {
	bucket := &sessionBucket{
		sessions:   make(map[string]*appencryption.Session),
		closeSpies: make(map[string]*closeSpy),
	}

	for i := 0; i < count; i++ {
		bucket.newSession(strconv.Itoa(i))
	}

	return bucket
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

	cache := appencryption.NewSessionCache(b.load, appencryption.NewCryptoPolicy())
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

	cache := appencryption.NewSessionCache(b.load, policy)
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

	cache := appencryption.NewSessionCache(b.load, policy)
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

func TestNewSessionCacheRistretto(t *testing.T) {
	loader := func(id string) (*appencryption.Session, error) {
		return &appencryption.Session{}, nil
	}

	cache := appencryption.NewSessionCacheX(loader, appencryption.NewCryptoPolicy())

	require.NotNil(t, cache)

	cache.Close()
}

func TestSessionCacheRistrettoGetUsesLoader(t *testing.T) {
	session, _ := newSessionWithMockEncryption()

	loader := func(id string) (*appencryption.Session, error) {
		return session, nil
	}

	cache := appencryption.NewSessionCacheX(loader, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	val, err := cache.Get("some-id")
	require.NoError(t, err)
	assert.Same(t, session, val)
}

func TestSessionCacheRistrettoCount(t *testing.T) {
	totalSessions := 10
	b := newSessionBucket(totalSessions)

	cache := appencryption.NewSessionCacheX(b.load, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	assert.Eventually(t, func() bool { return totalSessions == cache.Count() }, time.Second, time.Millisecond*10)
}

func TestSessionCacheRistrettoMaxCount(t *testing.T) {
	totalSessions := 20
	maxSessions := 10
	b := newSessionBucket(totalSessions)

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheSize = maxSessions

	cache := appencryption.NewSessionCacheX(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	assert.Eventually(t, func() bool { return maxSessions == cache.Count() }, time.Second, time.Millisecond*10)
}

func TestSessionCacheRistrettoTTL(t *testing.T) {
	ttl := time.Second
	totalSessions := 16
	b := newSessionBucket(totalSessions)

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheTTL = ttl

	cache := appencryption.NewSessionCacheX(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	b.fillAndAssertCacheContents(t, cache)

	// ensure the ttl has elapsed
	time.Sleep(ttl + time.Millisecond*50)

	// At this point all of the original 16 should be gone (or on their way out)
	assert.Eventually(t, func() bool { return cache.Count() == 0 }, time.Second*10, time.Millisecond*10)
}

func TestSharedSessionRistrettoCloseOnCacheClose(t *testing.T) {
	totalSessions := 1
	b := newSessionBucket(totalSessions)

	cache := appencryption.NewSessionCacheX(b.load, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	// b.fillAndAssertCacheContents(t, cache)
	s, err := cache.Get("0")
	require.NoError(t, err)
	require.NotNil(t, s)

	cache.Close()

	assert.Eventually(t, func() bool {
		return b.IsClosed("0")
	}, time.Second*10, time.Millisecond*500)
}

func TestSharedSessionRistrettoCloseOnEviction(t *testing.T) {
	totalSessions := 1
	b := newSessionBucket(totalSessions)

	policy := appencryption.NewCryptoPolicy()
	policy.SessionCacheSize = 1

	cache := appencryption.NewSessionCacheX(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	s1, err := cache.Get("0")
	require.NoError(t, err)
	require.NotNil(t, s1)
	assert.Eventually(t, func() bool {
		return cache.Count() == 1
	}, time.Second*10, time.Millisecond*500)

	s2, err := cache.Get("1")
	require.NoError(t, err)
	require.NotNil(t, s2)

	assert.Eventually(t, func() bool {
		return b.IsClosed("0")
	}, time.Second*10, time.Millisecond*500)

	assert.False(t, b.IsClosed("1"))
}

func TestSharedSessionRistrettoClose(t *testing.T) {
	b := newSessionBucket(0)

	cache := appencryption.NewSessionCacheX(b.load, appencryption.NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	s1, err := cache.Get("my-item")
	require.NoError(t, err)
	require.NotNil(t, s1)
	assert.Eventually(t, func() bool {
		return cache.Count() == 1
	}, time.Second*10, time.Millisecond*500)

	s1.Close()

	assert.Eventually(t, func() bool {
		return b.IsClosed("my-item")
	}, time.Second*10, time.Millisecond*500)
}

func TestSharedSessionRistrettoNotClosedWhenInUse(t *testing.T) {
	b := newSessionBucket(0)

	cache := appencryption.NewSessionCacheX(b.load, appencryption.NewCryptoPolicy())
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

	assert.False(t, b.IsClosed("my-item"))
}
