package appencryption

import (
	"fmt"
	"strconv"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption/pkg/log"
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
	closeSpies map[*Session]*closeSpy
	mu         sync.Mutex
}

func (b *sessionBucket) load(_ string) (*Session, error) {
	b.mu.Lock()
	defer b.mu.Unlock()

	return b.newSession(), nil
}

func (b *sessionBucket) newSession() *Session {
	spy := &closeSpy{}

	s := newSessionWithMockEncryption(func(_ mock.Arguments) {
		b.mu.Lock()
		defer b.mu.Unlock()

		spy.SetClosed()
	})

	b.closeSpies[s] = spy

	return s
}

func (b *sessionBucket) IsClosed(s *Session) bool {
	b.mu.Lock()
	defer b.mu.Unlock()

	if spy, ok := b.closeSpies[s]; ok {
		return spy.IsClosed()
	}

	return false
}

func newSessionBucket() *sessionBucket {
	return &sessionBucket{
		closeSpies: make(map[*Session]*closeSpy),
	}
}

func newSessionWithMockEncryption(callbacks ...func(mock.Arguments)) *Session {
	s := new(Session)
	m := new(MockEncryption)
	call := m.On("Close").Return(nil)

	for i := range callbacks {
		call = call.Run(callbacks[i])
	}

	sessionInjectEncryption(s, m)

	return s
}

func TestNewSessionCache(t *testing.T) {
	loader := func(id string) (*Session, error) {
		return &Session{}, nil
	}

	cache := newSessionCache(loader, NewCryptoPolicy())
	defer cache.Close()

	require.NotNil(t, cache)
}

func TestSessionCacheGetUsesLoader(t *testing.T) {
	session := newSessionWithMockEncryption()

	loader := func(id string) (*Session, error) {
		return session, nil
	}

	cache := newSessionCache(loader, NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	val, err := cache.Get("some-id")
	require.NoError(t, err)
	assert.Same(t, session, val)
}

func TestSessionCacheGetDoesNotUseLoaderOnHit(t *testing.T) {
	calls := 0
	session := newSessionWithMockEncryption()

	loader := func(id string) (*Session, error) {
		calls++

		return session, nil
	}

	cache := newSessionCache(loader, NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	val, err := cache.Get("some-id")
	require.NoError(t, err)
	assert.Same(t, session, val)
	assert.Equal(t, 1, calls, "loader expected to be called once, but it was called %d times", calls)

	// ensure the first item made it into the cache
	assert.Eventually(t, func() bool {
		return cache.Count() == 1
	}, time.Second*10, time.Millisecond*10)

	val2, err := cache.Get("some-id")
	require.NoError(t, err)
	assert.Same(t, val, val2)
	assert.Equal(t, 1, calls, "loader expected to be called once, but it was called %d times", calls)
}

func TestSessionCacheGetReturnLoaderError(t *testing.T) {
	loader := func(id string) (*Session, error) {
		return nil, assert.AnError
	}

	cache := newSessionCache(loader, NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	val, err := cache.Get("some-id")
	assert.Nil(t, val)
	assert.EqualError(t, err, assert.AnError.Error())
}

func TestSessionCacheCount(t *testing.T) {
	totalSessions := 10
	b := newSessionBucket()

	cache := newSessionCache(b.load, NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	for i := 0; i < totalSessions; i++ {
		cache.Get(strconv.Itoa(i))
	}

	assert.Eventually(t, func() bool { return totalSessions == cache.Count() }, time.Second, time.Millisecond*10)
}

func TestSessionCacheMaxCount(t *testing.T) {
	totalSessions := 20
	maxSessions := 10
	b := newSessionBucket()

	policy := NewCryptoPolicy()
	policy.SessionCacheMaxSize = maxSessions

	cache := newSessionCache(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	sessions := make([]*Session, totalSessions)

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
}

func TestSessionCacheDuration(t *testing.T) {
	ttl := time.Millisecond * 100

	// can't use more than 16 sessions here as that is the max drain
	// for the mango cache implementation
	totalSessions := 16
	b := newSessionBucket()

	policy := NewCryptoPolicy()
	policy.SessionCacheDuration = ttl

	cache := newSessionCache(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	for i := 0; i < totalSessions; i++ {
		cache.Get(strconv.Itoa(i))
	}

	// ensure the ttl has elapsed
	time.Sleep(ttl + time.Millisecond*50)

	expectedCount := 0

	// mango cache implementation only reaps expired entries following a write, so we'll write a new
	// cache entry and ensure it's the only one left
	_, _ = cache.Get("99") // IDs 0-15 were created above
	expectedCount = 1

	assert.Eventually(t, func() bool {
		return cache.Count() == expectedCount
	}, time.Second*10, time.Millisecond*10)
}

type testLogger struct {
	strings.Builder
}

func (t *testLogger) Debugf(f string, v ...interface{}) {
	t.Builder.WriteString(fmt.Sprintf(f, v...))
}

func TestSessionCacheCloseWithDebugLogging(t *testing.T) {
	b := newSessionBucket()

	cache := newSessionCache(b.load, NewCryptoPolicy())
	require.NotNil(t, cache)

	l := new(testLogger)
	assert.Equal(t, 0, l.Len())

	// enable debug logging and caputure
	log.SetLogger(l)

	cache.Close()

	// assert additional debug info was written to log
	assert.NotEqual(t, 0, l.Len())
	assert.Contains(t, l.String(), "session cache stash len = 0")

	log.SetLogger(nil)
}

func TestSharedSessionCloseOnCacheClose(t *testing.T) {
	b := newSessionBucket()

	cache := newSessionCache(b.load, NewCryptoPolicy())
	require.NotNil(t, cache)

	s, err := cache.Get("my-item")
	require.NoError(t, err)
	require.NotNil(t, s)
	s.Close()

	assert.Eventually(t, func() bool {
		return cache.Count() == 1
	}, time.Second*10, time.Millisecond*100)

	assert.False(t, b.IsClosed(s))

	cache.Close()

	assert.Eventually(t, func() bool {
		return b.IsClosed(s)
	}, time.Second*10, time.Millisecond*100)
}

func TestSharedSessionCloseOnEviction(t *testing.T) {
	b := newSessionBucket()

	const max = 10

	policy := NewCryptoPolicy()
	policy.SessionCacheMaxSize = max

	var firstBatch [max]*Session

	cache := newSessionCache(b.load, policy)
	require.NotNil(t, cache)

	defer cache.Close()

	for i := 0; i < max; i++ {
		s, err := cache.Get(strconv.Itoa(i))
		require.NoError(t, err)
		require.NotNil(t, s)
		s.Close()

		firstBatch[i] = s
	}

	assert.Eventually(t, func() bool {
		return cache.Count() == max
	}, time.Second*10, time.Millisecond*100)

	s2, err := cache.Get(strconv.Itoa(max + 1))
	require.NoError(t, err)
	require.NotNil(t, s2)
	s2.Close()

	assert.Eventually(t, func() bool {
		count := 0

		// One--and only one--of the first batch items should be closed
		for _, f := range firstBatch {
			if b.IsClosed(f) {
				count++
			}
		}

		return count == 1
	}, time.Second*10, time.Millisecond*100)

	assert.False(t, b.IsClosed(s2))
}

func TestSharedSessionCloseDoesNotCloseUnderlyingSession(t *testing.T) {
	b := newSessionBucket()

	cache := newSessionCache(b.load, NewCryptoPolicy())
	require.NotNil(t, cache)

	defer cache.Close()

	s1, err := cache.Get("my-item")
	require.NoError(t, err)
	require.NotNil(t, s1)

	// close the session
	s1.Close()

	assert.Eventually(t, func() bool {
		return cache.Count() == 1
	}, time.Second*10, time.Millisecond*100)

	time.Sleep(time.Millisecond * 200)

	// shared sessions aren't actually closed until evicted from the cache
	assert.False(t, b.IsClosed(s1))
}

func TestCacheStash(t *testing.T) {
	id := "stashed item"
	stash := newCacheStash()

	complete := make(chan bool)

	go func() {
		stash.process()

		complete <- true
	}()

	// stash is empty
	s, ok := stash.get(id)
	assert.Nil(t, s)
	assert.False(t, ok)
	assert.Equal(t, 0, stash.len())

	// create a new session and stash it
	sess := new(Session)
	stash.add(id, sess)

	// stash now contains the session we just added
	s, ok = stash.get(id)
	assert.Equal(t, sess, s)
	assert.True(t, ok)
	assert.Equal(t, 1, stash.len())

	// now remove the stashed session
	stash.remove(id)

	// remove events are queued asynchronously
	assert.Eventually(t, func() bool {
		_, ok := stash.get(id)

		return !ok
	}, 500*time.Millisecond, 10*time.Millisecond)

	// and verify it's gone
	s, ok = stash.get(id)
	assert.Nil(t, s)
	assert.False(t, ok)
	assert.Equal(t, 0, stash.len())

	stash.close()
	assert.True(t, <-complete)
}
