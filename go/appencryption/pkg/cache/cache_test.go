package cache_test

import (
	"testing"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache"
)

type CacheSuite struct {
	suite.Suite
	cache cache.Interface[int, string]
}

func TestCacheSuite(t *testing.T) {
	suite.Run(t, new(CacheSuite))
}

func (suite *CacheSuite) SetupTest() {
	suite.cache = cache.New[int, string](2)
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
