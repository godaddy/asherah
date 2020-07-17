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
	inner  mango.LoadingCache
	loader SessionLoaderFunc
}

func (m *mangoCache) Get(id string) (*Session, error) {
	sess, err := m.get(id)
	if err != nil {
		return nil, err
	}

	incrementSharedSessionUsage(sess)

	return sess, nil
}

func (m *mangoCache) get(id string) (*Session, error) {
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
	s.encryption.(*SharedEncryption).incrementUsage()
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

func mangoRemovalListener(_ mango.Key, v mango.Value) {
	go v.(*Session).encryption.(*SharedEncryption).Remove()
}

func newMangoCache(sessionLoader SessionLoaderFunc, policy *CryptoPolicy) *mangoCache {
	return &mangoCache{
		loader: sessionLoader,
		inner: mango.NewLoadingCache(
			func(k mango.Key) (mango.Value, error) {
				return sessionLoader(k.(string))
			},
			mango.WithMaximumSize(policy.SessionCacheSize),
			mango.WithExpireAfterAccess(policy.SessionCacheTTL),
			mango.WithRemovalListener(mangoRemovalListener),
		),
	}
}

// SharedEncryption is used to track the number of concurrent users to ensure sessions remain
// cached while in use.
type SharedEncryption struct {
	Encryption

	accessCounter int
	mu            *sync.Mutex
	cond          *sync.Cond

	closed bool
}

func (s *SharedEncryption) incrementUsage() {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.accessCounter++
}

func (s *SharedEncryption) Close() error {
	s.mu.Lock()
	defer s.mu.Unlock()
	defer s.cond.Broadcast()

	s.accessCounter--
	if s.accessCounter == 0 {
		s.closed = true
	}

	return nil
}

func (s *SharedEncryption) Remove() {
	s.mu.Lock()

	for !s.closed {
		s.cond.Wait()
	}

	s.Encryption.Close()

	s.mu.Unlock()
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

		_, ok := s.encryption.(*SharedEncryption)
		if !ok {
			mu := new(sync.Mutex)
			orig := s.encryption
			wrapped := &SharedEncryption{
				Encryption: orig,
				mu:         mu,
				cond:       sync.NewCond(mu),
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
