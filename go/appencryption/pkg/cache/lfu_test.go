package cache_test

import (
	"fmt"
	"testing"
	"time"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type LFUSuite struct {
	suite.Suite
	cache cache.Interface[int, string]
}

func TestLFUSuite(t *testing.T) {
	suite.Run(t, new(LFUSuite))
}

func (suite *LFUSuite) SetupTest() {
	suite.cache = cache.New[int, string](2).LFU().Build()
}

func (suite *LFUSuite) TestNewLFU() {
	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(2, suite.cache.Capacity())
}

func (suite *LFUSuite) TestSet() {
	suite.cache.Set(1, "one")
	suite.Assert().Equal(1, suite.cache.Len())

	suite.cache.Set(2, "two")
	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Set(3, "three")
	suite.Assert().Equal(2, suite.cache.Len())
}

func (suite *LFUSuite) TestGet() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	one, ok := suite.cache.Get(1)
	suite.Assert().Equal("one", one)
	suite.Assert().True(ok)

	two, ok := suite.cache.Get(2)
	suite.Assert().Equal("two", two)
	suite.Assert().True(ok)

	val, ok := suite.cache.Get(3)
	suite.Assert().False(ok)
	suite.Assert().Equal("", val)
}

func (suite *LFUSuite) TestGetOrPanic() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal("one", suite.cache.GetOrPanic(1))
	suite.Assert().Equal("two", suite.cache.GetOrPanic(2))

	suite.Assert().Panics(func() { suite.cache.GetOrPanic(3) })
}

func (suite *LFUSuite) TestDelete() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal(2, suite.cache.Len())

	// ensure the key is deleted and the size is decremented
	ok := suite.cache.Delete(1)
	suite.Assert().True(ok)
	suite.Assert().Equal(1, suite.cache.Len())

	// subsequent delete should return false
	ok = suite.cache.Delete(1)
	suite.Assert().False(ok)

	// ensure the key is no longer in the cache
	one, ok := suite.cache.Get(1)
	suite.Assert().Equal("", one)
	suite.Assert().False(ok)

	suite.cache.Delete(2)
	suite.Assert().Equal(0, suite.cache.Len())
}

// func (suite *LFUSuite) TestEach() {
// 	suite.cache.Set(1, "one")
// 	suite.cache.Set(1, "one") // increment frequency
// 	suite.cache.Set(2, "two")
// 	suite.cache.Set(3, "three") // evict 2

// 	var (
// 		keys   []int
// 		values []string
// 	)

// 	suite.cache.Each(func(key int, value string) bool {
// 		keys = append(keys, key)
// 		values = append(values, value)

// 		return true
// 	})

// 	// Each() iterates in order of least frequently used
// 	suite.Assert().Equal([]int{3, 1}, keys)
// 	suite.Assert().Equal([]string{"three", "one"}, values)
// }

// func (suite *LFUSuite) TestEachWithEarlyExit() {
// 	suite.cache.Set(1, "one")
// 	suite.cache.Set(1, "one") // increment frequency
// 	suite.cache.Set(2, "two")
// 	suite.cache.Set(3, "three") // evict 2

// 	var (
// 		keys   []int
// 		values []string
// 	)

// 	suite.cache.Each(func(key int, value string) bool {
// 		keys = append(keys, key)
// 		values = append(values, value)

// 		// early exit
// 		return false
// 	})

// 	// Each() iterates in order of least frequently used
// 	suite.Assert().Equal([]int{3}, keys)
// 	suite.Assert().Equal([]string{"three"}, values)
// }

func (suite *LFUSuite) TestEviction() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	// access 1 to increase frequency
	suite.cache.Set(1, "one")

	suite.cache.Set(3, "three")

	_, ok := suite.cache.Get(1)
	suite.Assert().True(ok)

	// 2 should be evicted as it has the lowest frequency
	_, ok = suite.cache.Get(2)
	suite.Assert().False(ok)

	_, ok = suite.cache.Get(3)
	suite.Assert().True(ok)
}

func (suite *LFUSuite) TestClose() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.cache.Close()

	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(0, suite.cache.Capacity())
}

func (suite *LFUSuite) TestWithEvictFunc() {
	evicted := map[int]int{}

	suite.cache = cache.New[int, string](100).
		WithEvictFunc(func(key int, _ string) {
			evicted[key] = 1
		}).
		LFU().
		Build()

	// overfill the cache
	for i := 0; i < 105; i++ {
		suite.cache.Set(i, fmt.Sprintf("value-%d", i))
	}

	// wait for the background goroutine to evict items
	suite.Assert().Eventually(func() bool {
		return len(evicted) == 5
	}, 100*time.Millisecond, 10*time.Millisecond, "eviction callback was not called")

	// verify the first five items were evicted
	for i := 0; i < 5; i++ {
		suite.Assert().Contains(evicted, i)
	}

	// close the cache and evict the remaining items
	suite.cache.Close()

	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(105, len(evicted))
}
