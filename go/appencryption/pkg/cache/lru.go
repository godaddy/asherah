//nolint:forcetypeassert // we know the type of the value
package cache

import (
	"container/list"
)

// lru is a least recently used cache policy implementation.
type lru[K comparable, V any] struct {
	cap       int
	evictList *list.List
}

// Init initializes the LRU cache policy.
func (c *lru[K, V]) Init(capacity int) {
	c.cap = capacity
	c.evictList = list.New()
}

// Capacity returns the capacity of the cache.
func (c *lru[K, V]) Capacity() int {
	return c.cap
}

// len returns the number of items in the cache.
func (c *lru[K, V]) len() int {
	return c.evictList.Len()
}

// Access is called when an item is accessed in the cache. It moves the item to
// the front of the eviction list.
func (c *lru[K, V]) Access(item *cacheItem[K, V]) {
	c.evictList.MoveToFront(item.parent)
}

// Admit is called when an item is added to the cache. It adds the item to the
// front of the eviction list.
func (c *lru[K, V]) Admit(item *cacheItem[K, V]) {
	item.parent = c.evictList.PushFront(item)
}

// Remove is called when an item is removed from the cache. It removes the item
// from the eviction list.
func (c *lru[K, V]) Remove(item *cacheItem[K, V]) {
	c.evictList.Remove(item.parent)
}

// Victim returns the least recently used item in the cache.
func (c *lru[K, V]) Victim() *cacheItem[K, V] {
	oldest := c.evictList.Back()
	if oldest == nil {
		return nil
	}

	return oldest.Value.(*cacheItem[K, V])
}

// Close implements the policy interface.
func (c *lru[K, V]) Close() {
	c.evictList = nil
	c.cap = 0
}

const protectedRatio = 0.8

// slruItem is an item in the SLRU cache.
type slruItem[K comparable, V any] struct {
	*cacheItem[K, V]
	protected bool
}

// slru is a Segmented LRU cache policy implementation.
type slru[K comparable, V any] struct {
	cap int

	protectedCapacity int
	protectedList     *list.List

	probationCapacity int
	probationList     *list.List
}

// Init initializes the SLRU cache policy.
func (c *slru[K, V]) Init(capacity int) {
	c.cap = capacity

	c.protectedList = list.New()
	c.probationList = list.New()

	c.protectedCapacity = int(float64(capacity) * protectedRatio)
	c.probationCapacity = capacity - c.protectedCapacity
}

// Capacity returns the capacity of the cache.
func (c *slru[K, V]) Capacity() int {
	return c.cap
}

// Access is called when an item is accessed in the cache. It moves the item to
// the front of its respective eviction list.
func (c *slru[K, V]) Access(item *cacheItem[K, V]) {
	sitem := item.parent.Value.(*slruItem[K, V])
	if sitem.protected {
		c.protectedList.MoveToFront(item.parent)
		return
	}

	// must be in probation list, promote to protected list
	sitem.protected = true

	c.probationList.Remove(item.parent)

	item.parent = c.protectedList.PushFront(sitem)

	// if the protected list is too big, demote the oldest item to the probation list
	if c.protectedList.Len() > c.protectedCapacity {
		b := c.protectedList.Back()
		c.protectedList.Remove(b)

		bitem := b.Value.(*slruItem[K, V])
		bitem.protected = false

		bitem.parent = c.probationList.PushFront(bitem)
	}
}

// Admit is called when an item is added to the cache. It adds the item to the
// front of the probation list.
func (c *slru[K, V]) Admit(item *cacheItem[K, V]) {
	newItem := &slruItem[K, V]{
		cacheItem: item,
		protected: false,
	}

	item.parent = c.probationList.PushFront(newItem)
}

// Victim returns the least recently used item in the cache.
func (c *slru[K, V]) Victim() *cacheItem[K, V] {
	if c.probationList.Len() > 0 {
		return c.probationList.Back().Value.(*slruItem[K, V]).cacheItem
	}

	if c.protectedList.Len() > 0 {
		return c.protectedList.Back().Value.(*slruItem[K, V]).cacheItem
	}

	return nil
}

// Remove is called when an item is removed from the cache. It removes the item
// from the eviction list.
func (c *slru[K, V]) Remove(item *cacheItem[K, V]) {
	sitem := item.parent.Value.(*slruItem[K, V])
	if sitem.protected {
		c.protectedList.Remove(item.parent)
		return
	}

	c.probationList.Remove(item.parent)
}

// Close implements the policy interface.
func (c *slru[K, V]) Close() {
	c.protectedList = nil
	c.probationList = nil
	c.cap = 0
}
