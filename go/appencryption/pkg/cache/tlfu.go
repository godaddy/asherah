package cache

import (
	"github.com/godaddy/asherah/go/appencryption/pkg/cache/internal"
)

const (
	samplesMultiplier        = 8
	insertionsMultiplier     = 2
	countersMultiplier       = 1
	falsePositiveProbability = 0.1
	admissionRatio           = 0.01
)

// tinyLFUEntry is an entry in the tinyLFU cache.
type tinyLFUEntry[K comparable, V any] struct {
	hash   uint64
	parent policy[K, V]
}

// tinyLFU is a tiny LFU cache policy implementation derived from
// [Mango Cache] and based on the algorithm described in the paper
// ["TinyLFU: A Highly Efficient Cache Admission Policy"] by Gil Einziger,
// Roy Friedman, and Ben Manes.
//
// [Mango Cache]: https://github.com/goburrow/cache
// ["TinyLFU: A Highly Efficient Cache Admission Policy"]: https://arxiv.org/pdf/1512.00727v2.pdf
type tinyLFU[K comparable, V any] struct {
	cap int

	filter  internal.BloomFilter    // 1bit counter
	counter internal.CountMinSketch // 4bit counter

	additions int
	samples   int

	lru  lru[K, V]
	slru slru[K, V]

	keys map[K]tinyLFUEntry[K, V] // Hashmap containing *tinyLFUEntry for O(1) access
}

// Init initializes the tinyLFU cache policy.
func (c *tinyLFU[K, V]) Init(capacity int) {
	c.cap = capacity

	c.keys = make(map[K]tinyLFUEntry[K, V])

	c.samples = capacity * samplesMultiplier

	c.filter.Init(capacity*insertionsMultiplier, falsePositiveProbability)
	c.counter.Init(capacity * countersMultiplier)

	// The admission window is a fixed percentage of the cache capacity.
	// The LRU is the first part of the admission window, and the SLRU is
	// the second part.
	//
	// Note that for small cache sizes the admission window may be 0, in which
	// case the SLRU is the entire cache and the doorkeeper is not used.
	lruCap := int(float64(capacity) * admissionRatio)
	c.lru.Init(lruCap)

	slruCap := capacity - lruCap
	c.slru.Init(slruCap)
}

// Capacity returns the capacity of the cache.
func (c *tinyLFU[K, V]) Capacity() int {
	return c.cap
}

// Access is called when an item is accessed in the cache. It increments the
// frequency of the item.
func (c *tinyLFU[K, V]) Access(item *cacheItem[K, V]) {
	c.increment(item)

	c.keys[item.key].parent.Access(item)
}

// Admit is called when an item is added to the cache. It increments the
// frequency of the item.
func (c *tinyLFU[K, V]) Admit(item *cacheItem[K, V]) {
	if c.bypassed() {
		c.slru.Admit(item)
		return
	}

	c.increment(item)

	// If there's room in the admission window, add it to the LRU
	if c.lru.len() < c.lru.cap {
		c.admitTo(item, &c.lru)

		return
	}

	victim := c.lru.Victim()

	// Otherwise, promote the victim from the LRU to the SLRU
	c.lru.Remove(victim)
	c.admitTo(victim, &c.slru)

	// then add the new item to the LRU
	c.admitTo(item, &c.lru)
}

// bypassed returns true if the doorkeeper is not in use.
func (c *tinyLFU[K, V]) bypassed() bool {
	return c.lru.cap == 0
}

// admitTo adds the item to the provided eviction list.
func (c *tinyLFU[K, V]) admitTo(item *cacheItem[K, V], list policy[K, V]) {
	list.Admit(item)

	c.keys[item.key] = tinyLFUEntry[K, V]{
		hash:   internal.ComputeHash(item.key),
		parent: list,
	}
}

// Victim returns the victim item to be evicted.
func (c *tinyLFU[K, V]) Victim() *cacheItem[K, V] {
	candidate := c.lru.Victim()

	// If the LRU is empty, just return the SLRU victim.
	// This is the case when the cache is closing and
	// the items are being purged.
	if candidate == nil {
		return c.slru.Victim()
	}

	victim := c.slru.Victim()

	// If the SLRU is empty, just return the LRU victim.
	if victim == nil {
		return candidate
	}

	// we have both a candidate and a victim
	// ...may the best item win!
	candidateFreq := c.estimate(c.keys[candidate.key].hash)
	victimFreq := c.estimate(c.keys[victim.key].hash)

	// If the candidate is more frequently accessed than the victim,
	// remove the candidate from the LRU and add it to the SLRU.
	if candidateFreq > victimFreq {
		c.lru.Remove(candidate)

		c.admitTo(candidate, &c.slru)

		return victim
	}

	return candidate
}

// estimate returns the estimated frequency of the item.
func (c *tinyLFU[K, V]) estimate(h uint64) uint8 {
	freq := c.counter.Estimate(h)
	if c.filter.Contains(h) {
		freq++
	}

	return freq
}

// Remove is called when an item is removed from the cache. It removes the item
// from the appropriate eviction list.
func (c *tinyLFU[K, V]) Remove(item *cacheItem[K, V]) {
	c.keys[item.key].parent.Remove(item)
}

// increment increments the frequency of the item.
func (c *tinyLFU[K, V]) increment(item *cacheItem[K, V]) {
	if c.bypassed() {
		return
	}

	c.additions++

	if c.additions >= c.samples {
		c.filter.Reset()
		c.counter.Reset()

		c.additions = 0
	}

	k := c.keys[item.key]

	if c.filter.Put(k.hash) {
		c.counter.Add(k.hash)
	}
}

// Close removes all items from the cache.
func (c *tinyLFU[K, V]) Close() {
	c.lru.Close()
	c.slru.Close()

	c.cap = 0
}
