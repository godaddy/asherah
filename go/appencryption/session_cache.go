package appencryption

import (
	"sync"
	"time"

	mango "github.com/goburrow/cache"

	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

type sessionCache interface {
	Get(id string) (*Session, error)
	Count() int
	Close()
}

// cacheStash is a temporary staging ground for the session cache.
type cacheStash struct {
	tmp    map[string]*Session
	mux    sync.RWMutex
	events chan stashEvent
}

type event uint8

const (
	stashClose event = iota
	stashRemove
)

type stashEvent struct {
	id    string
	event event
}

func (c *cacheStash) process() {
	for e := range c.events {
		switch e.event {
		case stashRemove:
			c.mux.Lock()
			delete(c.tmp, e.id)
			c.mux.Unlock()
		case stashClose:
			close(c.events)

			return
		}
	}
}

func (c *cacheStash) add(id string, s *Session) {
	c.mux.Lock()
	c.tmp[id] = s
	c.mux.Unlock()
}

func (c *cacheStash) get(id string) (s *Session, ok bool) {
	c.mux.RLock()
	s, ok = c.tmp[id]
	c.mux.RUnlock()

	return s, ok
}

func (c *cacheStash) remove(id string) {
	c.events <- stashEvent{
		id:    id,
		event: stashRemove,
	}
}

func (c *cacheStash) close() {
	c.events <- stashEvent{
		event: stashClose,
	}
}

func (c *cacheStash) len() int {
	c.mux.RLock()
	defer c.mux.RUnlock()

	return len(c.tmp)
}

func newCacheStash() *cacheStash {
	return &cacheStash{
		tmp:    make(map[string]*Session),
		events: make(chan stashEvent),
	}
}

// mangoCache is a sessionCache implementation based on goburrow's
// Mango cache (https://github.com/goburrow/cache).
type mangoCache struct {
	inner  mango.LoadingCache
	loader sessionLoaderFunc

	// mu protects the inner queue
	mu sync.Mutex

	stash *cacheStash
}

func (m *mangoCache) Get(id string) (*Session, error) {
	m.mu.Lock()
	defer m.mu.Unlock()

	sess, err := m.getOrAdd(id)
	if err != nil {
		return nil, err
	}

	incrementSharedSessionUsage(sess)

	return sess, nil
}

func (m *mangoCache) getOrAdd(id string) (*Session, error) {
	// (fast path) if it's cached return it immediately
	if val, ok := m.inner.GetIfPresent(id); ok {
		sess := sessionOrPanic(val)

		m.stash.remove(id)

		return sess, nil
	}

	// check the stash first to prevent mango from reloading a value currently in queue to be cached.
	if sess, ok := m.stash.get(id); ok {
		return sess, nil
	}

	// m.inner.Get will add a new item via the loader on cache miss. However, newly loaded keys are added to
	// the cache asynchronously, so we'll need to add it to the stash down below.
	val, err := m.inner.Get(id)
	if err != nil {
		return nil, err
	}

	sess := sessionOrPanic(val)

	// if we're here then mango has loaded a new cache value (session), so we'll add it to the tmp cache for now to
	// allow mango an opportunity to actually cache the value.
	m.stash.add(id, sess)

	return sess, nil
}

func sessionOrPanic(val mango.Value) *Session {
	sess, ok := val.(*Session)
	if !ok {
		panic("unexpected value")
	}

	return sess
}

func incrementSharedSessionUsage(s *Session) {
	s.encryption.(*sharedEncryption).incrementUsage()
}

func (m *mangoCache) Count() int {
	s := &mango.Stats{}
	m.inner.Stats(s)

	return int(s.LoadSuccessCount - s.EvictionCount)
}

func (m *mangoCache) Close() {
	if log.DebugEnabled() {
		s := &mango.Stats{}
		m.inner.Stats(s)
		log.Debugf("session cache stash len = %d\n", m.stash.len())
		log.Debugf("%v\n", s)
	}

	m.inner.Close()
	m.stash.close()
}

func mangoRemovalListener(m *mangoCache, k mango.Key, v mango.Value) {
	m.stash.remove(k.(string))

	go v.(*Session).encryption.(*sharedEncryption).Remove()
}

func newMangoCache(sessionLoader sessionLoaderFunc, policy *CryptoPolicy) *mangoCache {
	cache := &mangoCache{
		loader: sessionLoader,
		stash:  newCacheStash(),
	}

	cache.inner = mango.NewLoadingCache(
		func(k mango.Key) (mango.Value, error) {
			return sessionLoader(k.(string))
		},
		mango.WithMaximumSize(policy.SessionCacheMaxSize),
		mango.WithExpireAfterAccess(policy.SessionCacheDuration),
		mango.WithRemovalListener(func(k mango.Key, v mango.Value) {
			mangoRemovalListener(cache, k, v)
		}),
	)

	go cache.stash.process()

	return cache
}

// sharedEncryption is used to track the number of concurrent users to ensure sessions remain
// cached while in use.
type sharedEncryption struct {
	Encryption

	created       time.Time
	accessCounter int
	mu            *sync.Mutex
	cond          *sync.Cond
}

func (s *sharedEncryption) incrementUsage() {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.accessCounter++
}

func (s *sharedEncryption) Close() error {
	s.mu.Lock()
	defer s.mu.Unlock()
	defer s.cond.Broadcast()

	s.accessCounter--

	return nil
}

func (s *sharedEncryption) Remove() {
	s.mu.Lock()

	for s.accessCounter > 0 {
		s.cond.Wait()
	}

	s.Encryption.Close()

	s.mu.Unlock()
}

// sessionLoaderFunc retrieves a Session corresponding to the given partition ID.
type sessionLoaderFunc func(id string) (*Session, error)

// newSessionCache returns a new SessionCache with the configured cache implementation
// using the provided SessionLoaderFunc and CryptoPolicy.
func newSessionCache(loader sessionLoaderFunc, policy *CryptoPolicy) sessionCache {
	wrapper := func(id string) (*Session, error) {
		s, err := loader(id)
		if err != nil {
			return nil, err
		}

		_, ok := s.encryption.(*sharedEncryption)
		if !ok {
			mu := new(sync.Mutex)
			orig := s.encryption
			wrapped := &sharedEncryption{
				Encryption: orig,
				mu:         mu,
				cond:       sync.NewCond(mu),
				created:    time.Now(),
			}

			sessionInjectEncryption(s, wrapped)
		}

		return s, nil
	}

	return newMangoCache(wrapper, policy)
}

// sessionInjectEncryption is used to inject e into s and is primarily used for testing.
func sessionInjectEncryption(s *Session, e Encryption) {
	log.Debugf("injecting Encryption(%p) into Session(%p)", e, s)

	s.encryption = e
}
