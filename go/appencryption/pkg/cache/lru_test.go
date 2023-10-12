package cache_test

import (
	"fmt"
	"testing"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type LRUSuite struct {
	suite.Suite
	cache cache.Interface[int, string]
}

func TestLRUSuite(t *testing.T) {
	suite.Run(t, new(LRUSuite))
}

func (suite *LRUSuite) SetupTest() {
	suite.cache = cache.New[int, string](10)
}

func (suite *LRUSuite) TestNewLRU() {
	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(10, suite.cache.Capacity())
}

func (suite *LRUSuite) TestSet() {
	// fill to capacity
	for i := 0; i < suite.cache.Capacity(); i++ {
		suite.cache.Set(i, fmt.Sprintf("#%d", i))
	}

	// verify size
	suite.Assert().Equal(suite.cache.Capacity(), suite.cache.Len())
}

func (suite *LRUSuite) TestGet() {
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

func (suite *LRUSuite) TestGetOrPanic() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	one := suite.cache.GetOrPanic(1)
	suite.Assert().Equal("one", one)

	two := suite.cache.GetOrPanic(2)
	suite.Assert().Equal("two", two)

	suite.Assert().Panics(func() { suite.cache.GetOrPanic(3) })
}

func (suite *LRUSuite) TestDelete() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Delete(1)
	suite.Assert().Equal(1, suite.cache.Len())

	suite.cache.Delete(2)
	suite.Assert().Equal(0, suite.cache.Len())
}

func (suite *LRUSuite) TestClose() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Close()
	suite.Assert().Equal(0, suite.cache.Len())
}

func (suite *LRUSuite) TestEviction() {
	// fill the cache to capacity
	for i := 0; i < suite.cache.Capacity(); i++ {
		suite.cache.Set(i, fmt.Sprintf("#%d", i))
	}

	// access the first item to make it the most recently used
	suite.cache.Get(0)

	// add a new item to the cache
	suite.cache.Set(10, "#10")

	// the least recently used item should have been evicted
	_, ok := suite.cache.Get(1)
	suite.Assert().False(ok)

	// the most recently used item should still be in the cache
	_, ok = suite.cache.Get(0)
	suite.Assert().True(ok)

	suite.Assert().Equal(10, suite.cache.Len())
}

func (suite *LRUSuite) TestWithEvictFunc() {
	done := make(chan struct{})

	evicted := false
	cache := cache.New[int, string](1, cache.WithEvictFunc(func(key int, value string) {
		evicted = true

		suite.Assert().Equal(1, key)
		suite.Assert().Equal("one", value)

		close(done)
	}))

	cache.Set(1, "one")
	cache.Set(2, "two")

	<-done

	suite.Assert().True(evicted)
	suite.Assert().Equal(1, cache.Len())
}

type SLRUSuite struct {
	suite.Suite
	cache cache.Interface[int, string]
}

func TestSLRUSuite(t *testing.T) {
	suite.Run(t, new(SLRUSuite))
}

func (suite *SLRUSuite) SetupTest() {
	suite.cache = cache.New[int, string](10, cache.WithPolicy[int, string](cache.SLRU))
}

func (suite *SLRUSuite) TestNewSLRU() {
	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(10, suite.cache.Capacity())
}

func (suite *SLRUSuite) TestSet() {
	suite.cache.Set(1, "one")
	suite.Assert().Equal(1, suite.cache.Len())

	suite.cache.Set(2, "two")
	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Set(3, "three")
	suite.Assert().Equal(3, suite.cache.Len())
}

func (suite *SLRUSuite) TestGet() {
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

func (suite *SLRUSuite) TestGetOrPanic() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	one := suite.cache.GetOrPanic(1)
	suite.Assert().Equal("one", one)

	two := suite.cache.GetOrPanic(2)
	suite.Assert().Equal("two", two)

	suite.Assert().Panics(func() { suite.cache.GetOrPanic(3) })
}

func (suite *SLRUSuite) TestDelete() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Delete(1)
	suite.Assert().Equal(1, suite.cache.Len())

	suite.cache.Delete(2)
	suite.Assert().Equal(0, suite.cache.Len())
}

func (suite *SLRUSuite) TestClose() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	// throw in a get for good measure
	suite.cache.Get(1)

	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Close()
	suite.Assert().Equal(0, suite.cache.Len())
}

func (suite *SLRUSuite) TestCloseEmpty() {
	suite.cache.Close()
}

func (suite *SLRUSuite) TestEviction() {
	// fill the cache to capacity
	for i := 0; i < suite.cache.Capacity(); i++ {
		suite.cache.Set(i, fmt.Sprintf("#%d", i))
	}

	// access the first item to make it the most recently used
	suite.cache.Get(0)

	// add a new item to the cache
	suite.cache.Set(10, "#10")

	// the least recently used item should have been evicted
	_, ok := suite.cache.Get(1)
	suite.Assert().False(ok)

	// the most recently used item should still be in the cache
	_, ok = suite.cache.Get(0)
	suite.Assert().True(ok)

	// verify other items are still in the cache
	for i := 2; i < suite.cache.Capacity(); i++ {
		_, ok := suite.cache.Get(i)
		suite.Assert().True(ok)
	}

	suite.Assert().Equal(10, suite.cache.Len())
}

func (suite *SLRUSuite) TestWithEvictFunc() {
	done := make(chan struct{})

	evicted := false
	cache := cache.New[int, string](10, cache.WithPolicy[int, string](cache.SLRU), cache.WithEvictFunc(func(key int, value string) {
		evicted = true

		suite.Assert().Equal(1, key)
		suite.Assert().Equal("#1", value)

		close(done)
	}))

	// fill the cache to capacity
	for i := 0; i < cache.Capacity(); i++ {
		cache.Set(i, fmt.Sprintf("#%d", i))
	}

	// access the first item to make it the most recently used
	cache.Get(0)

	// add a new item to the cache
	cache.Set(10, "#10")

	<-done

	suite.Assert().True(evicted)
	suite.Assert().Equal(10, cache.Len())
}
