// Package cache provides a cache implementation with support for multiple
// eviction policies.
//
// Currently supported eviction policies:
//   - LRU (least recently used)
//   - LFU (least frequently used)
//   - SLRU (segmented least recently used)
//   - TinyLFU (tiny least frequently used)
//
// The cache is safe for concurrent access.
package cache

import (
	"container/list"
	"fmt"
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// Interface is intended to be a generic interface for cache implementations.
type Interface[K comparable, V any] interface {
	Get(key K) (V, bool)
	GetOrPanic(key K) V
	Set(key K, value V)
	Delete(key K) bool
	Len() int
	Capacity() int
	Close() error
}

// CachePolicy is an enum for the different eviction policies.
type CachePolicy string

const (
	// LRU is the least recently used cache policy.
	LRU CachePolicy = "lru"
	// LFU is the least frequently used cache policy.
	LFU CachePolicy = "lfu"
	// SLRU is the segmented least recently used cache policy.
	SLRU CachePolicy = "slru"
	// TinyLFU is the tiny least frequently used cache policy.
	TinyLFU CachePolicy = "tinylfu"
	// DefaultCachePolicy is the default cache policy.
	DefaultCachePolicy = LRU
)

// String returns the string representation of the eviction policy.
func (e CachePolicy) String() string {
	return string(e)
}

// EvictFunc is called when an item is evicted from the cache. The key and
// value of the evicted item are passed to the function.
type EvictFunc[K comparable, V any] func(key K, value V)

// NopEvict is a no-op EvictFunc.
func NopEvict[K comparable, V any](K, V) {}

// event is the cache event (evictItem or closeCache).
type event int

const (
	// evictItem is sent on the events channel when an item is evicted from the cache.
	evictItem event = iota
	// closeCache is sent on the events channel when the cache is closed.
	closeCache
)

type cacheItem[K comparable, V any] struct {
	key   K
	value V

	parent *list.Element // Pointer to the frequencyParent

	expiration time.Time // Expiration time
}

// cacheEvent is the event sent on the events channel.
type cacheEvent[K comparable, V any] struct {
	event event
	item  *cacheItem[K, V]
}

// policy is the generic interface for eviction policies.
type policy[K comparable, V any] interface {
	// init initializes the policy with the given capacity.
	init(int)
	// capacity returns the capacity of the policy.
	capacity() int
	// close removes all items from the cache, sends a close event to the event
	// processing goroutine, and waits for it to exit.
	close()
	// admit is called when an item is admitted to the cache.
	admit(item *cacheItem[K, V])
	// access is called when an item is access.
	access(item *cacheItem[K, V])
	// victim returns the victim item to be evicted.
	victim() *cacheItem[K, V]
	// remove is called when an item is remove from the cache.
	remove(item *cacheItem[K, V])
}

// Clock is an interface for getting the current time.
type Clock interface {
	Now() time.Time
}

// realClock is the default Clock implementation.
type realClock struct{}

// Now returns the current time.
func (c *realClock) Now() time.Time {
	return time.Now()
}

type builder[K comparable, V any] struct {
	capacity  int
	policy    policy[K, V]
	evictFunc EvictFunc[K, V]
	clock     Clock
	expiry    time.Duration
	isSync    bool
}

// New returns a new cache builder with the given capacity. Use the builder to
// set the eviction policy, eviction callback, and other options. Call Build()
// to create the cache.
func New[K comparable, V any](capacity int) *builder[K, V] {
	return &builder[K, V]{
		capacity:  capacity,
		policy:    new(lru[K, V]),
		evictFunc: NopEvict[K, V],
		clock:     new(realClock),
	}
}

// WithEvictFunc sets the EvictFunc for the cache.
func (b *builder[K, V]) WithEvictFunc(fn EvictFunc[K, V]) *builder[K, V] {
	b.evictFunc = fn

	return b
}

// WithPolicy sets the eviction policy for the cache. The default policy is LRU.
func (b *builder[K, V]) WithPolicy(policy CachePolicy) *builder[K, V] {
	switch policy {
	case LRU:
		b.policy = new(lru[K, V])
	case LFU:
		b.policy = new(lfu[K, V])
	case SLRU:
		b.policy = new(slru[K, V])
	case TinyLFU:
		b.policy = new(tinyLFU[K, V])
	default:
		panic(fmt.Sprintf("cache: unsupported policy \"%s\"", policy.String()))
	}

	return b
}

// LRU sets the cache eviction policy to LRU (least recently used).
func (b *builder[K, V]) LRU() *builder[K, V] {
	return b.WithPolicy(LRU)
}

// LFU sets the cache eviction policy to LFU (least frequently used).
func (b *builder[K, V]) LFU() *builder[K, V] {
	return b.WithPolicy(LFU)
}

// SLRU sets the cache eviction policy to SLRU (segmented least recently used).
func (b *builder[K, V]) SLRU() *builder[K, V] {
	return b.WithPolicy(SLRU)
}

// TinyLFU sets the cache eviction policy to TinyLFU (tiny least frequently used).
func (b *builder[K, V]) TinyLFU() *builder[K, V] {
	return b.WithPolicy(TinyLFU)
}

// WithClock sets the Clock for the cache.
func (b *builder[K, V]) WithClock(clock Clock) *builder[K, V] {
	b.clock = clock

	return b
}

// WithExpiry sets the expiry for the cache.
func (b *builder[K, V]) WithExpiry(expiry time.Duration) *builder[K, V] {
	b.expiry = expiry

	return b
}

// Synchronous sets the cache to use a synchronous eviction process. By
// default, the cache uses a concurrent eviction process which executes the
// eviction callback in a separate goroutine.
// Use this option to ensure eviction is processed inline, prior to adding
// a new item to the cache.
func (b *builder[K, V]) Synchronous() *builder[K, V] {
	b.isSync = true

	return b
}

// Build creates the cache.
func (b *builder[K, V]) Build() Interface[K, V] {
	c := &cache[K, V]{
		byKey: make(map[K]*cacheItem[K, V]),

		policy:          b.policy,
		clock:           b.clock,
		expiry:          b.expiry,
		onEvictCallback: b.evictFunc,
		isSync:          b.isSync,
	}

	c.policy.init(b.capacity)

	c.startup()

	return c
}

// cache is the generic cache type.
type cache[K comparable, V any] struct {
	byKey  map[K]*cacheItem[K, V] // Hashmap containing *CacheItems for O(1) access
	size   int                    // Current number of items in the cache
	events chan cacheEvent[K, V]  // Channel to events when an item is evicted
	policy policy[K, V]           // Eviction policy

	mux sync.RWMutex // synchronize access to the cache

	closing bool
	closeWG sync.WaitGroup

	// onEvictCallback is called when an item is evicted from the cache. The key, value,
	// and frequency of the evicted item are passed to the function. Set to
	// a custom function to handle evicted items. The default is a no-op.
	onEvictCallback EvictFunc[K, V]

	// clock is used to get the current time. Set to a custom Clock to use a
	// custom clock. The default is the real time clock.
	clock Clock

	// expiry is the duration after which an item is considered expired. Set to
	// a custom duration to use a custom expiry. The default is no expiry.
	expiry time.Duration

	// isSync is true if the cache uses a synchronized eviction process. The default
	// is false, which uses a concurrent eviction process.
	isSync bool
}

// processEvents processes events in a separate goroutine.
func (c *cache[K, V]) processEvents() {
	defer c.closeWG.Done()

	for event := range c.events {
		switch event.event {
		case evictItem:
			log.Debugf("%s executing evict callback for item: %v", c, event.item.key)
			c.onEvictCallback(event.item.key, event.item.value)
		case closeCache:
			log.Debugf("%s closed, exiting event loop", c)

			return
		}
	}
}

// Close the cache and remove all items. The cache cannot be used after it is
// closed.
func (c *cache[K, V]) Close() error {
	c.mux.Lock()
	defer c.mux.Unlock()

	// if the cache is already closed, do nothing
	if c.closing {
		return nil
	}

	c.closing = true

	for c.size > 0 {
		c.evict()
	}

	c.shutdown()

	c.byKey = nil

	c.policy.close()

	return nil
}

// startup starts the cache event processing goroutine.
func (c *cache[K, V]) startup() {
	if c.isSync {
		// no need to start the event processing goroutine
		return
	}

	c.events = make(chan cacheEvent[K, V])

	c.closeWG.Add(1)

	go c.processEvents()
}

// shutdown closes the events channel and waits for the event processing
// goroutine to exit.
func (c *cache[K, V]) shutdown() {
	if c.isSync {
		return
	}

	c.events <- cacheEvent[K, V]{event: closeCache}

	c.closeWG.Wait()

	close(c.events)

	c.events = nil
}

// Len returns the number of items in the cache.
func (c *cache[K, V]) Len() int {
	c.mux.RLock()
	defer c.mux.RUnlock()

	return c.size
}

// Capacity returns the maximum number of items in the cache.
func (c *cache[K, V]) Capacity() int {
	c.mux.RLock()
	defer c.mux.RUnlock()

	return c.policy.capacity()
}

// Set adds a value to the cache. If an item with the given key already exists,
// its value is updated.
func (c *cache[K, V]) Set(key K, value V) {
	c.mux.Lock()
	defer c.mux.Unlock()

	if c.closing {
		return
	}

	if item, ok := c.byKey[key]; ok {
		item.value = value

		if c.expiry > 0 {
			item.expiration = c.clock.Now().Add(c.expiry)
		}

		c.policy.access(item)

		return
	}

	// if the cache is full, evict an item
	if c.size == c.policy.capacity() {
		c.evict()
	}

	item := &cacheItem[K, V]{
		key:   key,
		value: value,
	}

	if c.expiry > 0 {
		item.expiration = c.clock.Now().Add(c.expiry)
	}

	c.byKey[key] = item

	c.size++

	c.policy.admit(item)
}

// Get returns a value from the cache. If an item with the given key does not
// exist, the second return value will be false.
func (c *cache[K, V]) Get(key K) (V, bool) {
	c.mux.Lock()
	defer c.mux.Unlock()

	if c.closing {
		return c.zeroValue(), false
	}

	item, ok := c.byKey[key]
	if !ok {
		return c.zeroValue(), false
	}

	if c.expiry > 0 && item.expiration.Before(c.clock.Now()) {
		c.evictItem(item)
		return c.zeroValue(), false
	}

	c.policy.access(item)

	return item.value, true
}

// GetOrPanic returns the value for the given key. If the key does not exist, a
// panic is raised.
func (c *cache[K, V]) GetOrPanic(key K) V {
	if item, ok := c.Get(key); ok {
		return item
	}

	panic(fmt.Sprintf("key does not exist: %v", key))
}

// Delete removes the given key from the cache. If the key does not exist, the
// return value is false.
func (c *cache[K, V]) Delete(key K) bool {
	c.mux.Lock()
	defer c.mux.Unlock()

	if c.closing {
		return false
	}

	item, ok := c.byKey[key]
	if !ok {
		return false
	}

	delete(c.byKey, key)

	c.size--

	c.policy.remove(item)

	return true
}

// zeroValue returns the zero value for type V.
func (c *cache[K, V]) zeroValue() V {
	var v V
	return v
}

// evict removes an item from the cache and sends an evict event or, if the
// cache uses a synchronized eviction process, calls the evict callback.
func (c *cache[K, V]) evict() {
	item := c.policy.victim()
	c.evictItem(item)
}

// evictItem removes the given item from the cache and sends an evict event.
func (c *cache[K, V]) evictItem(item *cacheItem[K, V]) {
	delete(c.byKey, item.key)

	c.size--

	c.policy.remove(item)

	if c.isSync {
		log.Debugf("%s executing evict callback for item (synchronous): %v", c, item.key)

		c.onEvictCallback(item.key, item.value)

		return
	}

	log.Debugf("%s sending evict event for item: %v", c, item.key)
	c.events <- cacheEvent[K, V]{event: evictItem, item: item}
}

// String returns a string representation of this cache.
func (c *cache[K, V]) String() string {
	return fmt.Sprintf("cache[%T, %T](%p)", *new(K), *new(V), c)
}
