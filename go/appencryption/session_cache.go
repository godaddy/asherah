package appencryption

import (
	"sync"

	mango "github.com/goburrow/cache"
)

type sessionCache interface {
	Get(id string) (*Session, error)
	Count() int
	Close()
}

// mangoCache is a sessionCache implementation based on goburrow's
// Mango cache (https://github.com/goburrow/cache).
type mangoCache struct {
	inner  mango.LoadingCache
	loader sessionLoaderFunc
}

func (m *mangoCache) Get(id string) (*Session, error) {
	sess, err := m.getOrAdd(id)
	if err != nil {
		return nil, err
	}

	incrementSharedSessionUsage(sess)

	return sess, nil
}

func (m *mangoCache) getOrAdd(id string) (*Session, error) {
	// m.inner.Get will add a new item via the loader on cache miss
	val, err := m.inner.Get(id)
	if err != nil {
		return nil, err
	}

	sess, ok := val.(*Session)
	if !ok {
		panic("unexpected value")
	}

	return sess, nil
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
	m.inner.Close()
}

func mangoRemovalListener(_ mango.Key, v mango.Value) {
	go v.(*Session).encryption.(*sharedEncryption).Remove()
}

func newMangoCache(sessionLoader sessionLoaderFunc, policy *CryptoPolicy) *mangoCache {
	return &mangoCache{
		loader: sessionLoader,
		inner: mango.NewLoadingCache(
			func(k mango.Key) (mango.Value, error) {
				return sessionLoader(k.(string))
			},
			mango.WithMaximumSize(policy.SessionCacheMaxSize),
			mango.WithExpireAfterAccess(policy.SessionCacheDuration),
			mango.WithRemovalListener(mangoRemovalListener),
		),
	}
}

// sharedEncryption is used to track the number of concurrent users to ensure sessions remain
// cached while in use.
type sharedEncryption struct {
	Encryption

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

	for s.accessCounter != 0 {
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
			}

			sessionInjectEncryption(s, wrapped)
		}

		return s, nil
	}

	switch eng := policy.SessionCacheEngine; eng {
	case "", "default", "ristretto":
		return newRistrettoCache(wrapper, policy)
	case "mango":
		return newMangoCache(wrapper, policy)
	default:
		panic("invalid session cache engine: " + eng)
	}
}

// sessionInjectEncryption is used to inject e into s and is primarily used for testing.
func sessionInjectEncryption(s *Session, e Encryption) {
	s.encryption = e
}
