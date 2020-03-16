package appencryption

import (
	"fmt"
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

// cacheEntry contains a key and the time it was loaded from the metastore.
type cacheEntry struct {
	loadedAt time.Time
	key      *internal.CryptoKey
}

// newCacheEntry returns a cacheEntry with the current time and key.
func newCacheEntry(k *internal.CryptoKey) *cacheEntry {
	return &cacheEntry{
		loadedAt: time.Now(),
		key:      k,
	}
}

// cacheKey formats an id and create timestamp to a usable
// key for storage in a cache.
func cacheKey(id string, create int64) string {
	return fmt.Sprintf("%s-%d", id, create)
}

// keyLoaderFunc is an adapter to allow the use of ordinary functions as key loaders.
// If f is a function with the appropriate signature, keyLoaderFunc(f) is a keyLoader
// that calls f.
type keyLoaderFunc func() (*internal.CryptoKey, error)

// Load calls f().
func (f keyLoaderFunc) Load() (*internal.CryptoKey, error) {
	return f()
}

// keyLoader is used by cache objects to retrieve keys on an as-needed basis.
type keyLoader interface {
	Load() (*internal.CryptoKey, error)
}

// keyReloader extends keyLoader by adding the ability to inspect loaded keys
// and reload them when needed
type keyReloader interface {
	keyLoader

	// IsInvalid returns true if the provided key is no longer valid
	IsInvalid(*internal.CryptoKey) bool
}

// cache contains cached keys for reuse.
type cache interface {
	GetOrLoad(id KeyMeta, loader keyLoader) (*internal.CryptoKey, error)
	GetOrLoadLatest(id string, loader keyLoader) (*internal.CryptoKey, error)
	Close() error
}

// Verify keyCache implements the cache interface.
var _ cache = (*keyCache)(nil)

// keyCache is used to persist session based keys and destroys them on a call to close.
type keyCache struct {
	once   sync.Once
	rw     sync.RWMutex
	policy *CryptoPolicy
	keys   map[string]*cacheEntry
}

// newKeyCache constructs a cache object that is ready to use.
func newKeyCache(policy *CryptoPolicy) *keyCache {
	return &keyCache{
		policy: policy,
		keys:   make(map[string]*cacheEntry),
	}
}

// isReloadRequired returns true if the check interval has elapsed
// since the timestamp provided.
func isReloadRequired(entry *cacheEntry, checkInterval time.Duration) bool {
	if entry.key.Revoked() {
		// this key is revoked so no need to reload it again.
		return false
	}

	return entry.loadedAt.Add(checkInterval).Before(time.Now())
}

// GetOrLoad returns a key from the cache if it's already been loaded. If the key
// is not present in the cache it will retrieve the key using the provided keyLoader
// and store the key if an error is not returned.
func (c *keyCache) GetOrLoad(id KeyMeta, loader keyLoader) (*internal.CryptoKey, error) {
	if k, ok := c.get(id); ok {
		return k, nil
	}

	return c.load(id, loader)
}

// get returns a key from the cache if present AND fresh.
// A cached value is considered stale if its time in cache
// has exceeded the RevokeCheckInterval.
// The second return value indicates the successful retrieval of a
// fresh key.
func (c *keyCache) get(id KeyMeta) (*internal.CryptoKey, bool) {
	c.rw.RLock()
	defer c.rw.RUnlock()

	key := cacheKey(id.ID, id.Created)

	if e, ok := c.keys[key]; ok && !isReloadRequired(e, c.policy.RevokeCheckInterval) {
		return e.key, true
	}

	return nil, false
}

// load returns a key from the cache if it's already been loaded. If the key is
// not present in the cache, or the cached entry needs to be reloaded, it will
// retrieve the key using the provided keyLoader and cache the key for future use.
// load maintains the latest entry for each distinct ID which can be accessed using
// id.Created == 0.
func (c *keyCache) load(id KeyMeta, loader keyLoader) (*internal.CryptoKey, error) {
	c.rw.Lock()
	defer c.rw.Unlock()

	key := cacheKey(id.ID, id.Created)

	e, ok := c.keys[key]
	if !ok || isReloadRequired(e, c.policy.RevokeCheckInterval) {
		k, err := loader.Load()
		if err != nil {
			return nil, err
		}

		if ok && e.key.Created() == k.Created() {
			// existing key in cache. update revoked status and last loaded time and close key
			// we just loaded since we don't need it
			e.key.SetRevoked(k.Revoked())
			e.loadedAt = time.Now()

			k.Close()
		} else {
			// first time loading this key into cache or we have an ID-only key with mismatched
			// create timestamps
			e = newCacheEntry(k)
			c.keys[key] = e
		}

		latestKey := cacheKey(id.ID, 0)
		if key == latestKey {
			// we've loaded a key using ID-only, ensure we've got a cache entry with a fully
			// qualified cache key
			c.keys[cacheKey(id.ID, k.Created())] = e
		} else if latest, ok := c.keys[latestKey]; !ok || latest.key.Created() < k.Created() {
			// we've loaded a key using a fully qualified cache key and the ID-only entry is
			// either missing or stale
			c.keys[latestKey] = e
		}
	}

	return e.key, nil
}

// GetOrLoadLatest returns the latest key from the cache matching the provided ID
// if it's already been loaded. If the key is not present in the cache it will
// retrieve the key using the provided KeyLoader and store the key if an error is not returned.
// If the provided loader implements the optional keyReloader interface then retrieved keys
// will be inspected for validity and reloaded if necessary.
func (c *keyCache) GetOrLoadLatest(id string, loader keyLoader) (*internal.CryptoKey, error) {
	key, err := c.GetOrLoad(KeyMeta{ID: id}, loader)
	if err != nil {
		return nil, err
	}

	if reloader, ok := loader.(keyReloader); ok && reloader.IsInvalid(key) {
		return loader.Load()
	}

	return key, nil
}

// Close frees all memory locked by the keys in this cache.
// It MUST be called after a session is complete to avoid
// running into MEMLOCK limits.
func (c *keyCache) Close() error {
	c.once.Do(c.close)
	return nil
}

func (c *keyCache) close() {
	for _, key := range c.keys {
		key.key.Close()
	}
}

// Verify neverCache implements the cache interface.
var _ cache = (*neverCache)(nil)

type neverCache struct {
}

// GetOrLoad always executes the provided function to load the value. It never actually caches.
func (neverCache) GetOrLoad(id KeyMeta, loader keyLoader) (*internal.CryptoKey, error) {
	return loader.Load()
}

// GetOrLoadLatest always executes the provided function to load the latest value. It never actually caches.
func (neverCache) GetOrLoadLatest(id string, loader keyLoader) (*internal.CryptoKey, error) {
	return loader.Load()
}

// Close is a no-op function to satisfy the cache interface
func (neverCache) Close() error {
	return nil
}
