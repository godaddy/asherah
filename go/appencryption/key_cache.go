package appencryption

import (
	"fmt"
	"strconv"
	"sync"
	"sync/atomic"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// cachedCryptoKey is a wrapper around a CryptoKey that tracks concurrent access.
// 
// Reference counting ensures proper cleanup:
// - Starts with ref count = 1 (owned by cache)
// - Incremented when retrieved via GetOrLoad
// - Decremented when caller calls Close()
// - When cache evicts, it removes from map THEN calls Close()
// - This prevents use-after-free since no new refs can be obtained
type cachedCryptoKey struct {
	*internal.CryptoKey

	refs *atomic.Int64
}

// newCachedCryptoKey returns a new cachedCryptoKey ready for use.
func newCachedCryptoKey(k *internal.CryptoKey) *cachedCryptoKey {
	ref := &atomic.Int64{}

	// initialize with a reference count of 1 to represent the
	// reference held by the cache
	ref.Add(1)

	return &cachedCryptoKey{
		CryptoKey: k,

		refs: ref,
	}
}

// Close decrements the reference count for this key. If the reference count
// reaches zero, the underlying key is closed.
// Returns true if the key was actually closed, false if there are still references.
func (c *cachedCryptoKey) Close() bool {
	newRefCount := c.refs.Add(-1)
	if newRefCount > 0 {
		return false
	}

	// newRefCount is 0, which means the ref count was 1 before decrement
	log.Debugf("closing cached key: %s, final ref count was 1", c.CryptoKey)
	c.CryptoKey.Close()
	return true
}

// increment the reference count for this key.
func (c *cachedCryptoKey) increment() {
	c.refs.Add(1)
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
		key:      newCachedCryptoKey(k),
	}
}

// simpleCache is a simple in-memory implementation of cache.Interface.
//
// It offers improved performance in scenarios where lockable memory is not a concern.
// Conversely, it is not recommended for memory bound systems, as it does not evict keys.
//
// Note: simpleCache is not safe for concurrent use and is intended to be used as a backend for keyCache, which is.
type simpleCache struct {
	m map[string]cacheEntry
}

// newSimpleCache returns a new simpleCache that is ready to use.
func newSimpleCache() *simpleCache {
	return &simpleCache{
		m: make(map[string]cacheEntry),
	}
}

// Get retrieves a cache entry from the cache if it exists.
func (s *simpleCache) Get(key string) (cacheEntry, bool) {
	value, ok := s.m[key]
	return value, ok
}

// GetOrPanic retrieves a cache entry from the cache if it exists, otherwise panics.
func (s *simpleCache) GetOrPanic(key string) cacheEntry {
	value, ok := s.m[key]
	if !ok {
		panic(fmt.Sprintf("key %s not found in cache", key))
	}
	return value
}

// Set stores a cache entry in the cache.
func (s *simpleCache) Set(key string, value cacheEntry) {
	s.m[key] = value
}

// Delete removes a cache entry from the cache.
func (s *simpleCache) Delete(key string) bool {
	_, ok := s.m[key]

	return ok
}

// Len returns the number of entries in the cache.
func (s *simpleCache) Len() int {
	return len(s.m)
}

// Capacity returns the maximum number of entries the cache can hold.
// The return value is -1 to indicate that the cache has no capacity limit.
func (s *simpleCache) Capacity() int {
	return -1
}

// Close closes the cache and frees all memory locked by the keys in this cache.
func (s *simpleCache) Close() error {
	for k, entry := range s.m {
		if !entry.key.Close() {
			log.Debugf("[simpleCache.Close] WARNING: failed to close key (still has references) -- id: %s, refs: %d\n", 
				k, entry.key.refs.Load())
		}
	}

	return nil
}

// cacheKey formats an id and create timestamp to a usable
// key for storage in a cache.
func cacheKey(id string, create int64) string {
	return id + strconv.FormatInt(create, 10)
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
	
	// orphaned tracks keys that were evicted from cache but still have references
	orphaned []*cachedCryptoKey
	orphanedMu sync.Mutex
	
	// cleanup management
	cleanupStop chan struct{}
	cleanupDone sync.WaitGroup
	cleanupOnce sync.Once
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

	switch t {
	case CacheTypeSystemKeys:
		cacheMaxSize = policy.SystemKeyCacheMaxSize
		cachePolicy = policy.SystemKeyCacheEvictionPolicy
	case CacheTypeIntermediateKeys:
		cacheMaxSize = policy.IntermediateKeyCacheMaxSize
		cachePolicy = policy.IntermediateKeyCacheEvictionPolicy
	}

	c = &keyCache{
		policy: policy,
		latest: make(map[string]KeyMeta),

		cacheType: t,
		orphaned: make([]*cachedCryptoKey, 0),
	}

	onEvict := func(key string, value cacheEntry) {
		log.Debugf("[onEvict] closing key -- id: %s\n", key)

		if !value.key.Close() {
			// Key still has active references and couldn't be closed.
			// Track it so we can clean it up later.
			c.orphanedMu.Lock()
			c.orphaned = append(c.orphaned, value.key)
			c.orphanedMu.Unlock()
			
			log.Debugf("[onEvict] WARNING: failed to close key (still has references) -- id: %s, refs: %d\n", 
				key, value.key.refs.Load())
			
			// NOTE: Orphaned keys are cleaned up by a background goroutine every 30 seconds.
			// This prevents memory accumulation while keeping cleanup out of the hot path.
			// In practice, orphaning should be rare as it only happens when a key is evicted
			// while still being actively used.
		}
	}

	if cachePolicy == "" || cachePolicy == "simple" {
		c.keys = newSimpleCache()
		// Start background cleanup for simple cache too
		c.startOrphanCleanup()
		return c
	}

	cb := cache.New[string, cacheEntry](cacheMaxSize)

	if cachePolicy != "" {
		log.Debugf("setting cache policy to %s", cachePolicy)

		cb.WithPolicy(cache.CachePolicy(cachePolicy))
	}

	if cacheMaxSize < 100 {
		log.Debugf("cache size is less than 100, setting synchronous eviction policy")

		cb.Synchronous()
	}

	c.keys = cb.WithEvictFunc(onEvict).Build()

	// Start background cleanup for orphaned keys
	c.startOrphanCleanup()

	return c
}

// startOrphanCleanup starts a background goroutine to periodically clean orphaned keys
func (c *keyCache) startOrphanCleanup() {
	c.cleanupStop = make(chan struct{})
	c.cleanupDone.Add(1)
	
	go func() {
		defer c.cleanupDone.Done()
		ticker := time.NewTicker(30 * time.Second)
		defer ticker.Stop()
		
		for {
			select {
			case <-ticker.C:
				c.cleanOrphaned()
			case <-c.cleanupStop:
				return
			}
		}
	}()
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
	c.rw.RLock()
	k, ok := c.getFresh(id)
	c.rw.RUnlock()

	if ok {
		return tracked(k), nil
	}

	c.rw.Lock()
	defer c.rw.Unlock()

	// exit early if the key doesn't need to be reloaded just in case it has
	// been loaded by rw lock in front of us
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

// cleanOrphaned attempts to close orphaned keys that no longer have references
func (c *keyCache) cleanOrphaned() {
	// Swap the list to minimize lock time
	c.orphanedMu.Lock()
	toClean := c.orphaned
	c.orphaned = make([]*cachedCryptoKey, 0)
	c.orphanedMu.Unlock()
	
	// Process outside the lock
	remaining := make([]*cachedCryptoKey, 0)
	for _, key := range toClean {
		if !key.Close() {
			// Still has references, keep it
			remaining = append(remaining, key)
		}
	}
	
	if len(toClean) > 0 && len(remaining) < len(toClean) {
		log.Debugf("%s cleaned up %d orphaned keys, %d still referenced\n", 
			c, len(toClean)-len(remaining), len(remaining))
	}
	
	// Put back the ones we couldn't close
	if len(remaining) > 0 {
		c.orphanedMu.Lock()
		c.orphaned = append(c.orphaned, remaining...)
		c.orphanedMu.Unlock()
	}
}

// Close frees all memory locked by the keys in this cache.
// It MUST be called after a session is complete to avoid
// running into MEMLOCK limits.
func (c *keyCache) Close() error {
	var closeErr error
	
	c.cleanupOnce.Do(func() {
		log.Debugf("%s closing\n", c)

		// Stop the cleanup goroutine
		if c.cleanupStop != nil {
			close(c.cleanupStop)
			c.cleanupDone.Wait()
		}

		// Clean up any orphaned keys first
		c.cleanOrphaned()
		
		// Close the cache
		closeErr = c.keys.Close()
		
		// Try once more to clean orphaned keys
		c.cleanOrphaned()
		
		if len(c.orphaned) > 0 {
			log.Debugf("%s WARNING: %d keys still orphaned with active references\n", c, len(c.orphaned))
		}
	})
	
	return closeErr
}

// String returns a string representation of this cache.
func (c *keyCache) String() string {
	return fmt.Sprintf("keyCache(%p){type=%s,size=%d,cap=%d}", c, c.cacheType, c.keys.Len(), c.keys.Capacity())
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

	return newCachedCryptoKey(k), nil
}

// GetOrLoadLatest always executes the provided function to load the latest value. It never actually caches.
func (neverCache) GetOrLoadLatest(id string, loader func(KeyMeta) (*internal.CryptoKey, error)) (*cachedCryptoKey, error) {
	k, err := loader(KeyMeta{ID: id})
	if err != nil {
		return nil, err
	}

	return newCachedCryptoKey(k), nil
}

// Close is a no-op function to satisfy the cache interface.
func (neverCache) Close() error {
	return nil
}
