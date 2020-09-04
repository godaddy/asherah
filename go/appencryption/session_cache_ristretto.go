package appencryption

import (
	"fmt"
	"time"

	"github.com/dgraph-io/ristretto"
)

// ristrettoCache is a SessionCache implementation based on dgraph-io's
// Ristretto cache library.
type ristrettoCache struct {
	inner   *ristretto.Cache
	loader  sessionLoaderFunc
	ttl     time.Duration
	maxSize int64
}

func (r *ristrettoCache) Get(id string) (*Session, error) {
	sess, err := r.getOrAdd(id)
	if err != nil {
		return nil, err
	}

	incrementSharedSessionUsage(sess)

	return sess, nil
}

func (r *ristrettoCache) getOrAdd(id string) (*Session, error) {
	if val, found := r.inner.Get(id); found {
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
		go s.encryption.(*sharedEncryption).Remove()
	}
}

func newRistrettoCache(sessionLoader sessionLoaderFunc, policy *CryptoPolicy) *ristrettoCache {
	capacity := int64(DefaultSessionCacheMaxSize)
	if policy.SessionCacheMaxSize > 0 {
		capacity = int64(policy.SessionCacheMaxSize)
	}

	conf := &ristretto.Config{
		NumCounters: 10 * capacity,
		MaxCost:     capacity,
		BufferItems: 64,
		Metrics:     true,
		OnEvict:     ristrettoOnEvict,
	}

	inner, err := ristretto.NewCache(conf)
	if err != nil {
		panic(fmt.Sprintf("unable to initialize cache: %s", err))
	}

	return &ristrettoCache{
		inner:   inner,
		loader:  sessionLoader,
		ttl:     policy.SessionCacheDuration,
		maxSize: capacity,
	}
}
