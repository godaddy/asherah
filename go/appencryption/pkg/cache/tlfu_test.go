package cache_test

import (
	"fmt"
	"testing"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type TinyLFUSuite struct {
	suite.Suite
	cache cache.Interface[int, string]
}

func TestTinyLFUSuite(t *testing.T) {
	// t.SkipNow()
	suite.Run(t, new(TinyLFUSuite))
}

func (suite *TinyLFUSuite) SetupTest() {
	suite.cache = cache.New[int, string](100, cache.WithPolicy[int, string](cache.TinyLFU))
}

func (suite *TinyLFUSuite) TestNewTinyLFU() {
	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(100, suite.cache.Capacity())
}

func (suite *TinyLFUSuite) TestSet() {
	// fill cache
	for i := 0; i < suite.cache.Capacity(); i++ {
		suite.cache.Set(i, fmt.Sprintf("%d", i))
	}

	suite.Assert().Equal(suite.cache.Capacity(), suite.cache.Len())

	// add one more
	suite.cache.Set(100, "one hundred")
	suite.Assert().Equal(suite.cache.Capacity(), suite.cache.Len())
}

func (suite *TinyLFUSuite) TestGet() {
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

func (suite *TinyLFUSuite) TestGetOrPanic() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	one := suite.cache.GetOrPanic(1)
	suite.Assert().Equal("one", one)

	two := suite.cache.GetOrPanic(2)
	suite.Assert().Equal("two", two)

	suite.Assert().Panics(func() { suite.cache.GetOrPanic(3) })
}

func (suite *TinyLFUSuite) TestDelete() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().True(suite.cache.Delete(1))
	suite.Assert().Equal(1, suite.cache.Len())

	suite.Assert().False(suite.cache.Delete(3))
	suite.Assert().Equal(1, suite.cache.Len())
}

func (suite *TinyLFUSuite) TestEvict() {
	// fill the cache to capacity
	for i := 0; i < suite.cache.Capacity(); i++ {
		suite.cache.Set(i, fmt.Sprintf("#%d", i))
	}

	// access half of the items
	for i := 0; i < suite.cache.Capacity()/2; i++ {
		_, ok := suite.cache.Get(i)
		suite.Assert().True(ok)
	}

	// add one more
	suite.cache.Set(999, "nine ninety nine")

	// access the new item
	_, ok := suite.cache.Get(999)
	suite.Assert().True(ok)

	// verify the cache is at capacity
	suite.Assert().Equal(suite.cache.Capacity(), suite.cache.Len())

	// overwrite half of the items
	for i := 0; i < suite.cache.Capacity(); i++ {
		key := i + 1000
		suite.cache.Set(key, fmt.Sprintf("##%d", key))
	}

	// verify 999 is still in the cache
	_, ok = suite.cache.Get(999)
	suite.Assert().True(ok, "item 999 should be in the cache")

	// verify all of the previously accessed items are still in the cache
	for i := 0; i < suite.cache.Capacity()/2; i++ {
		_, ok := suite.cache.Get(i)
		suite.Assert().True(ok, "item %d should be in the cache", i)
	}
}

func (suite *TinyLFUSuite) TestClose() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	suite.Assert().Equal(2, suite.cache.Len())

	suite.cache.Close()

	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(0, suite.cache.Capacity())
}
