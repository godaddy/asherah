package cache_test

import (
	"fmt"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

func ExampleNew() {
	evictionMsg := make(chan string)

	// This callback is executed via a background goroutine whenever an
	// item is evicted from the cache. We use a channel to synchronize
	// the goroutine with this example function so we can verify the
	// item that was evicted.
	evict := func(key int, value string) {
		evictionMsg <- fmt.Sprintln("evicted:", key, value)
	}

	// Create a new LFU cache with a capacity of 3 items and an eviction callback.
	cache := cache.New[int, string](3).LFU().WithEvictFunc(evict).Build()

	// Add some items to the cache.
	cache.Set(1, "foo")
	cache.Set(2, "bar")
	cache.Set(3, "baz")

	// Get an item from the cache.
	value, ok := cache.Get(1)
	if ok {
		fmt.Println("got:", value)
	}

	// Set a new value for an existing key
	cache.Set(2, "two")

	// Add another item to the cache which will evict the least frequently used
	// item (3).
	cache.Set(4, "qux")

	// Print the eviction message sent via the callback above.
	fmt.Print(<-evictionMsg)
	// Output:
	// got: foo
	// evicted: 3 baz
}
