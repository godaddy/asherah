package appencryption

import (
	"fmt"
	"sync"

	mango "github.com/goburrow/cache"
)

type SessionCache interface {
	Get(id string) (*Session, error)
	Count() int
	Close()
}

// mangoCache is a SessionCache implementation based on goburrow's
// Mango cache (https://github.com/goburrow/cache).
type mangoCache struct {
	inner mango.LoadingCache
}

func (m *mangoCache) Get(id string) (*Session, error) {
	val, err := m.inner.Get(id)
	if err != nil {
		return nil, err
	}

	sess, ok := val.(*Session)
	if !ok {
		panic("cached value not type *Session")
	}

	e, ok := sess.encryption.(*SharedEncryption)
	if !ok {
		panic("session.encryption should be wrapped")
	}

	e.incrementUsage()

	return sess, nil
}

func (m *mangoCache) Count() int {
	s := &mango.Stats{}
	m.inner.Stats(s)

	fmt.Printf("%+v\n", s)

	return int(s.LoadSuccessCount - s.EvictionCount)
}

func (m *mangoCache) Close() {
	m.inner.Close()
}

func (m *mangoCache) removalListener(k mango.Key, v mango.Value) {
	v.(*Session).Close()
}

func newMangoCache(sessionLoader SessionLoaderFunc, policy *CryptoPolicy) *mangoCache {
	loader := func(k mango.Key) (mango.Value, error) {
		return sessionLoader(k.(string))
	}

	mc := new(mangoCache)
	mc.inner = mango.NewLoadingCache(
		loader,
		mango.WithMaximumSize(policy.SessionCacheSize),
		mango.WithExpireAfterAccess(policy.SessionCacheTTL),
		mango.WithRemovalListener(mc.removalListener),
	)

	return mc
}

// SharedEncryption is used to track the number of concurrent users to ensure sessions remain
// cached while in use.
type SharedEncryption struct {
	Encryption

	accessCounter int
	mu            sync.Mutex
}

func (s *SharedEncryption) incrementUsage() {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.accessCounter++
}

func (s *SharedEncryption) Close() error {
	return s.close()
}

func (s *SharedEncryption) close() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.accessCounter--
	if s.accessCounter == 0 {
		return s.Encryption.Close()
	}

	return nil
}

// SessionLoaderFunc retrieves a Session corresponding to the given partition ID.
type SessionLoaderFunc func(id string) (*Session, error)

// NewSessionCache returns a new SessionCache with the configured cache implementation
// using the provided SessionLoaderFunc and CryptoPolicy.
func NewSessionCache(loader SessionLoaderFunc, policy *CryptoPolicy) SessionCache {
	wrapper := func(id string) (*Session, error) {
		s, err := loader(id)
		if err != nil {
			return nil, err
		}

		if _, ok := s.encryption.(*SharedEncryption); !ok {
			orig := s.encryption
			wrapped := &SharedEncryption{
				Encryption: orig,
			}

			SessionInjectEncryption(s, wrapped)
		}

		return s, nil
	}

	switch eng := policy.SessionCacheEngine; eng {
	case "", "default", "mango":
		return newMangoCache(wrapper, policy)
	case "ristretto":
		return newRistrettoCache(wrapper, policy)
	default:
		panic("invalid session cache engine: " + eng)
	}
}

// SessionInjectEncryption is used to inject e into s and is primarily used for testing.
func SessionInjectEncryption(s *Session, e Encryption) {
	s.encryption = e
}
