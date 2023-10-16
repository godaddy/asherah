package appencryption

import (
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

type sessionCache interface {
	Get(id string) (*Session, error)
	Count() int
	Close()
}

func incrementSharedSessionUsage(s *Session) {
	s.encryption.(*sharedEncryption).incrementUsage()
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

// sessionInjectEncryption is used to inject e into s and is primarily used for testing.
func sessionInjectEncryption(s *Session, e Encryption) {
	log.Debugf("injecting Encryption(%p) into Session(%p)", e, s)

	s.encryption = e
}

// newSessionCacheWithCache returns a new SessionCache with the provided cache implementation
// using the provided SessionLoaderFunc and CryptoPolicy.
func newSessionCacheWithCache(loader sessionLoaderFunc, policy *CryptoPolicy, cache cache.Interface[string, *Session]) sessionCache {
	return &cacheWrapper{
		loader: func(id string) (*Session, error) {
			log.Debugf("loading session for id: %s", id)

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
		},
		policy: policy,
		cache:  cache,
	}
}

// cacheWrapper is a wrapper around a cache.Interface[string, *Session] that implements the
// sessionCache interface.
type cacheWrapper struct {
	loader sessionLoaderFunc
	policy *CryptoPolicy
	cache  cache.Interface[string, *Session]

	mu sync.Mutex
}

func (c *cacheWrapper) Get(id string) (*Session, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	val, err := c.getOrAdd(id)
	if err != nil {
		return nil, err
	}

	incrementSharedSessionUsage(val)

	return val, nil
}

func (c *cacheWrapper) getOrAdd(id string) (*Session, error) {
	if val, ok := c.cache.Get(id); ok {
		return val, nil
	}

	val, err := c.loader(id)
	if err != nil {
		return nil, err
	}

	c.cache.Set(id, val)

	return val, nil
}

func (c *cacheWrapper) Count() int {
	return c.cache.Len()
}

func (c *cacheWrapper) Close() {
	log.Debugf("closing session cache")

	c.cache.Close()
}

func newSessionCache(loader sessionLoaderFunc, policy *CryptoPolicy) sessionCache {
	cb := cache.New[string, *Session](policy.SessionCacheMaxSize)
	cb.WithEvictFunc(func(k string, v *Session) {
		go v.encryption.(*sharedEncryption).Remove()
	})

	if policy.SessionCacheDuration > 0 {
		cb.WithExpiry(policy.SessionCacheDuration)
	}

	if policy.SessionCacheEvictionPolicy != "" {
		cb.WithPolicy(cache.CachePolicy(policy.SessionCacheEvictionPolicy))
	}

	return newSessionCacheWithCache(loader, policy, cb.Build())
}
