package cache_test

import (
	"testing"
	"time"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type CacheSuite struct {
	suite.Suite
	clock  *fakeClock
	expiry time.Duration
}

func TestCacheSuite(t *testing.T) {
	suite.Run(t, new(CacheSuite))
}

// fakeClock is a fake clock that returns a static time.
type fakeClock struct {
	now time.Time
}

// Now returns the current time.
func (c *fakeClock) Now() time.Time {
	return c.now
}

// SetNow sets the current time.
func (c *fakeClock) SetNow(now time.Time) {
	c.now = now
}

func (suite *CacheSuite) SetupTest() {
	suite.clock = &fakeClock{
		now: time.Now(),
	}

	suite.expiry = time.Hour
}

func (suite *CacheSuite) newCache() cache.Interface[int, string] {
	cb := cache.New[int, string](2).WithClock(suite.clock).WithExpiry(suite.expiry)

	return cb.Build()
}

func (suite *CacheSuite) TestBuild() {
	c := suite.newCache()

	suite.Assert().Equal(0, c.Len())
	suite.Assert().Equal(2, c.Capacity())
}

func (suite *CacheSuite) TestClosing() {
	c := suite.newCache()

	suite.Assert().NoError(c.Close())

	// set/get do nothing after closing
	c.Set(1, "one")
	suite.Assert().Equal(0, c.Len())

	// getting a value does nothing, returns false
	_, ok := c.Get(1)
	suite.Assert().False(ok)

	// delete does nothing
	suite.Assert().False(c.Delete(1))

	// closing again does nothing
	suite.Assert().NoError(c.Close())
}

func (suite *CacheSuite) TestExpiry() {
	c := suite.newCache()

	c.Set(1, "one")
	c.Set(2, "two")

	one, ok := c.Get(1)
	suite.Assert().Equal("one", one)
	suite.Assert().True(ok)

	two, ok := c.Get(2)
	suite.Assert().Equal("two", two)
	suite.Assert().True(ok)

	// advance clock
	suite.clock.SetNow(suite.clock.Now().Add(suite.expiry + time.Second))

	// get should return false
	_, ok = c.Get(1)
	suite.Assert().False(ok)

	_, ok = c.Get(2)
	suite.Assert().False(ok)
}

func (suite *CacheSuite) TestSynchronousEviction() {
	evicted := false

	cb := cache.New[int, string](2).Synchronous()
	cb.WithEvictFunc(func(key int, value string) {
		suite.Assert().Equal(1, key)
		suite.Assert().Equal("one", value)

		evicted = true
	})

	c := cb.Build()

	c.Set(1, "one")
	c.Set(2, "two")
	c.Set(3, "three")

	suite.Assert().True(evicted)

	// 1 should be evicted
	_, ok := c.Get(1)
	suite.Assert().False(ok)

	_, ok = c.Get(2)
	suite.Assert().True(ok)

	// 3 should still be there
	three, ok := c.Get(3)
	suite.Assert().Equal("three", three)
	suite.Assert().True(ok)
}

func (suite *CacheSuite) TestSynchronousClosing() {
	c := cache.New[int, string](2).Synchronous().Build()

	suite.Assert().NoError(c.Close())

	// set/get do nothing after closing
	c.Set(1, "one")
	suite.Assert().Equal(0, c.Len())

	// getting a value does nothing, returns false
	_, ok := c.Get(1)
	suite.Assert().False(ok)

	// delete does nothing
	suite.Assert().False(c.Delete(1))

	// closing again does nothing
	suite.Assert().NoError(c.Close())
}
