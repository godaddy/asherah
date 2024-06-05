//nolint:forcetypeassert // we know the type of the value
package cache

import (
	"container/list"
)

type frequencyParent[K comparable, V any] struct {
	entries   map[*cacheItem[K, V]]*list.Element // entries in this frequency to pointer to access list
	frequency int
	byAccess  *list.List // linked list of all entries in access order
}

// lfu implements a cache policy as described in
// ["An O(1) algorithm for implementing the lfu cache eviction scheme"].
//
// A cache utilizing this policy is safe for concurrent use and has a
// runtime complexity of O(1) for all operations.
//
// ["An O(1) algorithm for implementing the lfu cache eviction scheme"]: https://arxiv.org/pdf/2110.11602.pdf
type lfu[K comparable, V any] struct {
	cap         int
	frequencies *list.List // Linked list containing all frequencyParents in order of least frequently used
}

// Init initializes the LFU cache policy.
func (c *lfu[K, V]) Init(capacity int) {
	c.cap = capacity
	c.frequencies = list.New()
}

// Capacity returns the capacity of the cache.
func (c *lfu[K, V]) Capacity() int {
	return c.cap
}

// Access is called when an item is accessed in the cache. It increments the
// frequency of the item.
func (c *lfu[K, V]) Access(item *cacheItem[K, V]) {
	c.increment(item)
}

// Admit is called when an item is added to the cache. It increments the
// frequency of the item.
func (c *lfu[K, V]) Admit(item *cacheItem[K, V]) {
	c.increment(item)
}

// Remove is called when an item is removed from the cache. It removes the item
// from the frequency.
func (c *lfu[K, V]) Remove(item *cacheItem[K, V]) {
	c.delete(item.parent, item)
}

// Victim returns the least frequently used item in the cache.
func (c *lfu[K, V]) Victim() *cacheItem[K, V] {
	if frequency := c.frequencies.Front(); frequency != nil {
		elem := frequency.Value.(*frequencyParent[K, V]).byAccess.Front()
		if elem != nil {
			return elem.Value.(*cacheItem[K, V])
		}
	}

	return nil
}

// increment the frequency of the given item. If the frequency parent
// does not exist, it is created.
func (c *lfu[K, V]) increment(item *cacheItem[K, V]) {
	current := item.parent

	// next will be this item's new parent
	var next *list.Element

	// nextAmount will be the new frequency for this item
	var nextAmount int

	if current == nil {
		// the item has not yet been assigned a frequency so
		// this is the first time it is being accessed
		nextAmount = 1

		// set next to the first frequency
		next = c.frequencies.Front()
	} else {
		// increment the access frequency for the item
		nextAmount = current.Value.(*frequencyParent[K, V]).frequency + 1

		// set next to the next greater frequency
		next = current.Next()
	}

	// if the next frequency does not exist or the next frequency is not the
	// next frequency amount, create a new frequency item and insert it
	// after the current frequency
	if next == nil || next.Value.(*frequencyParent[K, V]).frequency != nextAmount {
		newFrequencyParent := &frequencyParent[K, V]{
			entries:   make(map[*cacheItem[K, V]]*list.Element),
			frequency: nextAmount,
			byAccess:  list.New(),
		}

		if current == nil {
			// current is nil so insert the new frequency item at the front
			next = c.frequencies.PushFront(newFrequencyParent)
		} else {
			// otherwise insert the new frequency item after the current
			next = c.frequencies.InsertAfter(newFrequencyParent, current)
		}
	}

	// set the item's parent to the next frequency
	item.parent = next

	// add the item to the frequency's access list
	nextAccess := next.Value.(*frequencyParent[K, V]).byAccess.PushBack(item)

	// add the item to the frequency's entries with a pointer to the access list
	next.Value.(*frequencyParent[K, V]).entries[item] = nextAccess

	// if the item was previously assigned a frequency, remove it from the
	// old frequency's entries
	if current != nil {
		c.delete(current, item)
	}
}

// delete removes the given item from the frequency and removes the frequency
// if it is empty.
func (c *lfu[K, V]) delete(frequency *list.Element, item *cacheItem[K, V]) {
	frequencyParent := frequency.Value.(*frequencyParent[K, V])

	// remove the item from the frequency's access list
	frequencyParent.byAccess.Remove(frequencyParent.entries[item])

	// remove the item from the frequency's entries
	delete(frequencyParent.entries, item)

	if len(frequencyParent.entries) == 0 {
		frequencyParent.entries = nil
		frequencyParent.byAccess = nil

		c.frequencies.Remove(frequency)
	}
}

// Close removes all items from the cache, sends a close event on the events
// channel, and waits for the cache to close.
func (c *lfu[K, V]) Close() {
	c.frequencies = nil
	c.cap = 0
}
