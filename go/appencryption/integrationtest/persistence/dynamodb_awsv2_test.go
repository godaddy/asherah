package persistence_test

import (
	"testing"

	"github.com/stretchr/testify/suite"
)

type DynamoDBV2Suite struct {
	DynamoDBSuite
}

func (suite *DynamoDBV2Suite) SetupTest() {
	suite.testContext.SeedDB(suite.T())
	suite.dynamodbMetastore = suite.testContext.NewMetastoreV2(false)
}

func TestDynamoDBV2Suite(t *testing.T) {
	if testing.Short() {
		t.Skip("too slow for testing.Short")
	}

	suite.Run(t, new(DynamoDBV2Suite))
}
