package cache_test

import (
	"testing"
	"time"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type CacheSuite struct {
	suite.Suite
	cache  cache.Interface[int, string]
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

	suite.cache = cache.New[int, string](2).WithClock(suite.clock).WithExpiry(suite.expiry).Build()
}

func (suite *CacheSuite) TestNew() {
	suite.Assert().Equal(0, suite.cache.Len())
	suite.Assert().Equal(2, suite.cache.Capacity())
}

func (suite *CacheSuite) TestClosing() {
	suite.Assert().NoError(suite.cache.Close())

	// set/get do nothing after closing
	suite.cache.Set(1, "one")
	suite.Assert().Equal(0, suite.cache.Len())

	// getting a value does nothing, returns false
	_, ok := suite.cache.Get(1)
	suite.Assert().False(ok)

	// delete does nothing
	suite.Assert().False(suite.cache.Delete(1))

	// closing again does nothing
	suite.Assert().NoError(suite.cache.Close())
}

func (suite *CacheSuite) TestExpiry() {
	suite.cache.Set(1, "one")
	suite.cache.Set(2, "two")

	one, ok := suite.cache.Get(1)
	suite.Assert().Equal("one", one)
	suite.Assert().True(ok)

	two, ok := suite.cache.Get(2)
	suite.Assert().Equal("two", two)
	suite.Assert().True(ok)

	// advance clock
	suite.clock.SetNow(suite.clock.Now().Add(suite.expiry + time.Second))

	// get should return false
	_, ok = suite.cache.Get(1)
	suite.Assert().False(ok)

	_, ok = suite.cache.Get(2)
	suite.Assert().False(ok)
}
