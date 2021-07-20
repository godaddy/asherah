package appencryption

import (
	"fmt"
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// cacheEntry contains a key and the time it was loaded from the metastore.
type cacheEntry struct {
	loadedAt time.Time
	key      *internal.CryptoKey
}

// newCacheEntry returns a cacheEntry with the current time and key.
func newCacheEntry(k *internal.CryptoKey) cacheEntry {
	return cacheEntry{
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
	keys   map[string]cacheEntry
}

// newKeyCache constructs a cache object that is ready to use.
func newKeyCache(policy *CryptoPolicy) *keyCache {
	keys := make(map[string]cacheEntry)

	return &keyCache{
		policy: policy,
		keys:   keys,
	}
}

// isReloadRequired returns true if the check interval has elapsed
// since the timestamp provided.
func isReloadRequired(entry cacheEntry, checkInterval time.Duration) bool {
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
	// get with "light" lock
	c.rw.RLock()
	k, ok := c.get(id)
	c.rw.RUnlock()

	if ok {
		return k, nil
	}

	// load with heavy lock
	c.rw.Lock()
	defer c.rw.Unlock()
	// exit early if the key doesn't need to be reloaded just in case it has been loaded by rw lock in front of us
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
	key := cacheKey(id.ID, id.Created)

	if e, ok := c.read(key); ok && !isReloadRequired(e, c.policy.RevokeCheckInterval) {
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
	key := cacheKey(id.ID, id.Created)

	k, err := loader.Load()
	if err != nil {
		return nil, err
	}

	e, ok := c.read(key)
	if ok && e.key.Created() == k.Created() {
		// existing key in cache. update revoked status and last loaded time and close key
		// we just loaded since we don't need it
		e.key.SetRevoked(k.Revoked())
		e.loadedAt = time.Now()
		c.write(key, e)

		k.Close()
	} else {
		// first time loading this key into cache or we have an ID-only key with mismatched
		// create timestamps
		e = newCacheEntry(k)
		c.write(key, e)
	}

	latestKey := cacheKey(id.ID, 0)
	if key == latestKey {
		// we've loaded a key using ID-only, ensure we've got a cache entry with a fully
		// qualified cache key
		c.write(cacheKey(id.ID, k.Created()), e)
	} else if latest, ok := c.read(latestKey); !ok || latest.key.Created() < k.Created() {
		// we've loaded a key using a fully qualified cache key and the ID-only entry is
		// either missing or stale
		c.write(latestKey, e)
	}

	return e.key, nil
}

// read retrieves the entry from the cache matching the provided ID if present. The second
// return value indicates whether or not the key was present in the cache.
func (c *keyCache) read(id string) (cacheEntry, bool) {
	e, ok := c.keys[id]

	if !ok {
		log.Debugf("%s miss -- id: %s\n", c, id)
	}

	return e, ok
}

// write entry e to the cache using id as the key.
func (c *keyCache) write(id string, e cacheEntry) {
	if existing, ok := c.keys[id]; ok {
		log.Debugf("%s update -> old: %s, new: %s, id: %s\n", c, existing.key, e.key, id)
	}

	log.Debugf("%s write -> key: %s, id: %s\n", c, e.key, id)
	c.keys[id] = e
}

// GetOrLoadLatest returns the latest key from the cache matching the provided ID
// if it's already been loaded. If the key is not present in the cache it will
// retrieve the key using the provided KeyLoader and store the key if an error is not returned.
// If the provided loader implements the optional keyReloader interface then retrieved keys
// will be inspected for validity and reloaded if necessary.
func (c *keyCache) GetOrLoadLatest(id string, loader keyLoader) (*internal.CryptoKey, error) {
	c.rw.Lock()
	defer c.rw.Unlock()

	meta := KeyMeta{ID: id}

	key, ok := c.get(meta)
	if !ok {
		log.Debugf("%s.GetOrLoadLatest get miss -- id: %s\n", c, id)

		var err error
		key, err = c.load(meta, loader)

		if err != nil {
			return nil, err
		}
	}

	if reloader, ok := loader.(keyReloader); ok && reloader.IsInvalid(key) {
		reloaded, ok := loader.Load()
		log.Debugf("%s.GetOrLoadLatest reload -- invalid: %s, new: %s, id: %s\n", c, key, reloaded, id)

		e := newCacheEntry(reloaded)

		// update latest
		latest := cacheKey(id, 0)
		c.write(latest, e)

		// ensure we've got a cache entry with a fully qualified cache key
		c.write(cacheKey(id, reloaded.Created()), e)

		return reloaded, ok
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
	c.rw.Lock()
	defer c.rw.Unlock()

	for k := range c.keys {
		c.keys[k].key.Close()
	}
}

func (c *keyCache) String() string {
	return fmt.Sprintf("keyCache(%p)", c)
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
