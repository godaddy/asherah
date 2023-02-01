package persistence

import (
	"testing"

	"github.com/stretchr/testify/suite"
)

// SQLSuite provides unit tests for the sql metastore implementation.
//
// Note that the integration tests formally provided by this package have been relocated to
// a new dedicated module: github.com/godaddy/asherah/go/appencryption/integrationtest.
type SQLSuite struct {
	suite.Suite
}

// TODO: add tests

func TestMySqlSuite(t *testing.T) {
	suite.Run(t, new(SQLSuite))
}
