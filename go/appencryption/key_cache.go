package appencryption

import (
	"fmt"
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// cachedCryptoKey is a wrapper around a CryptoKey that tracks concurrent access.
type cachedCryptoKey struct {
	*internal.CryptoKey

	rw   sync.RWMutex // protects concurrent access to the key's reference count
	refs int          // number of references to this key
}

// Close decrements the reference count for this key. If the reference count
// reaches zero, the underlying key is closed.
func (c *cachedCryptoKey) Close() {
	c.rw.Lock()
	defer c.rw.Unlock()

	c.refs--

	if c.refs > 0 {
		return
	}

	c.CryptoKey.Close()
}

// increment the reference count for this key.
func (c *cachedCryptoKey) increment() {
	c.rw.Lock()
	defer c.rw.Unlock()

	c.refs++
}

// cacheEntry contains a key and the time it was loaded from the metastore.
type cacheEntry struct {
	loadedAt time.Time
	key      *cachedCryptoKey
}

// newCacheEntry returns a cacheEntry with the current time and key.
func newCacheEntry(k *internal.CryptoKey) cacheEntry {
	return cacheEntry{
		loadedAt: time.Now(),
		key: &cachedCryptoKey{
			CryptoKey: k,

			// initialize with a reference count of 1 to represent the
			// reference held by the cache
			refs: 1,
		},
	}
}

// cacheKey formats an id and create timestamp to a usable
// key for storage in a cache.
func cacheKey(id string, create int64) string {
	return fmt.Sprintf("%s-%d", id, create)
}

// keyCacher contains cached keys for reuse.
type keyCacher interface {
	GetOrLoad(id KeyMeta, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error)
	GetOrLoadLatest(id string, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error)
	Close() error
}

// Verify keyCache implements the cache interface.
var _ keyCacher = (*keyCache)(nil)

// keyCache is used to persist session based keys and destroys them on a call to close.
type keyCache struct {
	policy *CryptoPolicy

	keys cache.Interface[string, cacheEntry]
	rw   sync.RWMutex // protects concurrent access to the cache

	latest map[string]KeyMeta

	cacheType cacheKeyType
}

// cacheKeyType is used to identify the type of key cache.
type cacheKeyType int

// String returns a string representation of the cacheKeyType.
func (t cacheKeyType) String() string {
	switch t {
	case CacheTypeSystemKeys:
		return "system"
	case CacheTypeIntermediateKeys:
		return "intermediate"
	default:
		return "unknown"
	}
}

const (
	// CacheTypeSystemKeys is used to cache system keys.
	CacheTypeSystemKeys cacheKeyType = iota
	// CacheTypeIntermediateKeys is used to cache intermediate keys.
	CacheTypeIntermediateKeys
)

// newKeyCache constructs a cache object that is ready to use.
func newKeyCache(t cacheKeyType, policy *CryptoPolicy) (c *keyCache) {
	cacheMaxSize := DefaultKeyCacheMaxSize
	cachePolicy := ""
	onEvict := func(key string, value cacheEntry) {
		log.Debugf("%s eviction -- key: %s, id: %s\n", c, value.key, key)

		value.key.Close()
	}

	switch t {
	case CacheTypeSystemKeys:
		cacheMaxSize = policy.SystemKeyCacheMaxSize
		cachePolicy = policy.SystemKeyCacheEvictionPolicy
	case CacheTypeIntermediateKeys:
		cacheMaxSize = policy.IntermediateKeyCacheMaxSize
		cachePolicy = policy.IntermediateKeyCacheEvictionPolicy
	}

	cb := cache.New[string, cacheEntry](cacheMaxSize)

	if cachePolicy != "" {
		cb.WithPolicy(cache.CachePolicy(cachePolicy))
	}

	keys := cb.WithEvictFunc(onEvict).Build()

	return &keyCache{
		policy: policy,
		keys:   keys,
		latest: make(map[string]KeyMeta),

		cacheType: t,
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
// is not present in the cache it will retrieve the key using the provided loader
// and store the key if an error is not returned.
func (c *keyCache) GetOrLoad(id KeyMeta, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	c.rw.Lock()
	defer c.rw.Unlock()

	if k, ok := c.getFresh(id); ok {
		return tracked(k), nil
	}

	k, err := c.load(id, loader)
	if err != nil {
		return nil, err
	}

	return tracked(k), nil
}

// tracked increments the reference count for the provided key, then returns it.
func tracked(key *cachedCryptoKey) *cachedCryptoKey {
	key.increment()
	return key
}

// getFresh returns a key from the cache if present AND fresh.
// A cached value is considered stale if its time in cache
// has exceeded the RevokeCheckInterval.
// The second return value indicates the successful retrieval of a
// fresh key.
func (c *keyCache) getFresh(meta KeyMeta) (*cachedCryptoKey, bool) {
	if e, ok := c.read(meta); ok && !isReloadRequired(e, c.policy.RevokeCheckInterval) {
		return e.key, true
	} else if ok {
		log.Debugf("%s stale -- id: %s-%d\n", c, meta.ID, e.key.Created())
		return e.key, false
	}

	return nil, false
}

// load retrieves a key using the provided loader. If the key is present in the cache
// it will be updated with the latest revocation status and last loaded time. Otherwise
// a new cache entry will be created and stored in the cache.
//
// load maintains the latest entry for each distinct KeyMeta.ID which can be accessed using
// KeyMeta.Created == 0.
func (c *keyCache) load(meta KeyMeta, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	k, err := loader(meta)
	if err != nil {
		return nil, err
	}

	e, ok := c.read(meta)

	switch {
	case ok:
		// existing key in cache. update revoked status and last loaded time and close key
		// we just loaded since we don't need it
		e.key.SetRevoked(k.Revoked())
		e.loadedAt = time.Now()

		k.Close()
	default:
		// first time loading this key into cache or we have an ID-only key with mismatched
		// create timestamps
		e = newCacheEntry(k)
	}

	c.write(meta, e)

	return e.key, nil
}

// read retrieves the entry from the cache matching the provided ID if present. The second
// return value indicates whether or not the key was present in the cache.
func (c *keyCache) read(meta KeyMeta) (cacheEntry, bool) {
	id := cacheKey(meta.ID, meta.Created)

	if meta.IsLatest() {
		if latest, ok := c.getLatestKeyMeta(meta.ID); ok {
			id = cacheKey(latest.ID, latest.Created)
		}
	}

	e, ok := c.keys.Get(id)
	if !ok {
		log.Debugf("%s miss -- id: %s\n", c, id)
	} else {
		log.Debugf("%s hit -- id: %s\n", c, id)
	}

	return e, ok
}

// getLatestKeyMeta returns the KeyMeta for the latest key for the provided ID.
// The second return value indicates whether or not the key was present in the cache.
func (c *keyCache) getLatestKeyMeta(id string) (KeyMeta, bool) {
	latest, ok := c.latest[cacheKey(id, 0)]

	return latest, ok
}

// mapLatestKeyMeta maps the provided latest KeyMeta to the provided ID.
func (c *keyCache) mapLatestKeyMeta(id string, latest KeyMeta) {
	c.latest[cacheKey(id, 0)] = latest
}

// write entry e to the cache using id as the key.
func (c *keyCache) write(meta KeyMeta, e cacheEntry) {
	if meta.IsLatest() {
		meta = KeyMeta{ID: meta.ID, Created: e.key.Created()}

		c.mapLatestKeyMeta(meta.ID, meta)
	} else if latest, ok := c.getLatestKeyMeta(meta.ID); !ok || latest.Created < e.key.Created() {
		c.mapLatestKeyMeta(meta.ID, meta)
	}

	id := cacheKey(meta.ID, meta.Created)

	if existing, ok := c.keys.Get(id); ok {
		log.Debugf("%s update -> old: %s, new: %s, id: %s\n", c, existing.key, e.key, id)
	}

	log.Debugf("%s write -> key: %s, id: %s\n", c, e.key, id)
	c.keys.Set(id, e)
}

// GetOrLoadLatest returns the latest key from the cache matching the provided ID
// if it's already been loaded. If the key is not present in the cache it will
// retrieve the key using the provided KeyLoader and store the key if successful.
// In the event that the cached or loaded key is invalid (see [keyCache.IsInvalid]),
// the key will be reloaded and the cache updated.
func (c *keyCache) GetOrLoadLatest(id string, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	c.rw.Lock()
	defer c.rw.Unlock()

	meta := KeyMeta{ID: id}

	key, ok := c.getFresh(meta)
	if !ok {
		log.Debugf("%s.GetOrLoadLatest get miss -- id: %s\n", c, id)

		var err error
		key, err = c.load(meta, loader)

		if err != nil {
			return nil, err
		}
	}

	if c.IsInvalid(key.CryptoKey) {
		reloaded, err := loader(meta)
		if err != nil {
			return nil, err
		}

		log.Debugf("%s.GetOrLoadLatest reload -- invalid: %s, new: %s, id: %s\n", c, key, reloaded, id)

		e := newCacheEntry(reloaded)

		// ensure we've got a cache entry with a fully qualified cache key
		c.write(KeyMeta{ID: id, Created: reloaded.Created()}, e)

		return tracked(e.key), nil
	}

	return tracked(key), nil
}

// IsInvalid returns true if the provided key is no longer valid.
func (c *keyCache) IsInvalid(key *internal.CryptoKey) bool {
	return internal.IsKeyInvalid(key, c.policy.ExpireKeyAfter)
}

// Close frees all memory locked by the keys in this cache.
// It MUST be called after a session is complete to avoid
// running into MEMLOCK limits.
func (c *keyCache) Close() error {
	return c.keys.Close()
}

// String returns a string representation of this cache.
func (c *keyCache) String() string {
	return fmt.Sprintf("keyCache(%p)", c)
}

// Verify neverCache implements the cache interface.
var _ keyCacher = (*neverCache)(nil)

type neverCache struct{}

// GetOrLoad always executes the provided function to load the value. It never actually caches.
func (neverCache) GetOrLoad(id KeyMeta, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	k, err := loader(id)
	if err != nil {
		return nil, err
	}

	return &cachedCryptoKey{CryptoKey: k}, nil
}

// GetOrLoadLatest always executes the provided function to load the latest value. It never actually caches.
func (neverCache) GetOrLoadLatest(id string, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	k, err := loader(KeyMeta{ID: id})
	if err != nil {
		return nil, err
	}

	return &cachedCryptoKey{CryptoKey: k}, nil
}

// Close is a no-op function to satisfy the cache interface.
func (neverCache) Close() error {
	return nil
}
