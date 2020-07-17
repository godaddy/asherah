package appencryption

import (
	"time"

	"github.com/dgraph-io/ristretto"
)

// NewSessionCacheX returns a new SessionCache with the default
// cache implementation configured using the provided SessionLoaderFunc
// and CryptoPolicy.
func NewSessionCacheX(loader SessionLoaderFunc, policy *CryptoPolicy) SessionCache {
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

	return newRistrettoCache(wrapper, policy)
}

// ristrettoCache is a SessionCache implementation based on dgraph-io's
// Ristretto cache library.
type ristrettoCache struct {
	inner   *ristretto.Cache
	loader  SessionLoaderFunc
	ttl     time.Duration
	maxSize int64
}

func (r *ristrettoCache) Get(id string) (*Session, error) {
	sess, err := r.getOrAdd(id)
	if err != nil {
		return nil, err
	}

	e, ok := sess.encryption.(*SharedEncryption)
	if !ok {
		panic("session.encryption should be wrapped")
	}

	e.incrementUsage()

	return sess, nil
}

func (r *ristrettoCache) getOrAdd(id string) (*Session, error) {
	val, found := r.inner.Get(id)
	if found {
		return val.(*Session), nil
	}

	sess, err := r.loader(id)
	if err != nil {
		return nil, err
	}

	r.inner.SetWithTTL(id, sess, 1, r.ttl)

	return sess, nil
}

func (r *ristrettoCache) Count() int {
	return int(r.inner.Metrics.KeysAdded() - r.inner.Metrics.KeysEvicted())
}

func (r *ristrettoCache) Close() {
	// TODO: better clean up needed
	// force eviction of all cache items by exausting the cache's
	added := r.inner.Set(-1, 0, r.maxSize)
	_ = added
}

func ristrettoOnEvict(h, c uint64, value interface{}, _ int64) {
	if s, ok := value.(*Session); ok {
		s.Close()
	}
}

func newRistrettoCache(sessionLoader SessionLoaderFunc, policy *CryptoPolicy) *ristrettoCache {
	capacity := int64(policy.SessionCacheSize)
	conf := &ristretto.Config{
		NumCounters: 10 * capacity,
		MaxCost:     capacity,
		BufferItems: 64,
		Metrics:     true,
		OnEvict:     ristrettoOnEvict,
	}

	inner, err := ristretto.NewCache(conf)
	if err != nil {
		panic("unable to initialize cache")
	}

	return &ristrettoCache{
		inner:   inner,
		loader:  sessionLoader,
		ttl:     policy.SessionCacheTTL,
		maxSize: capacity,
	}
}
