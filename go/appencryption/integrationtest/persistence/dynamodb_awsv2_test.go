package persistence_test

import (
	"testing"
	"time"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/integrationtest/dynamodbtest"
)

// DynamoDBV2Suite is the test suite for the AWS SDK v2 DynamoDBMetastore.
// Tests are run against a local DynamoDB instance using testcontainers with complete AWS SDK v2 infrastructure.
type DynamoDBV2Suite struct {
	suite.Suite

	instant           int64
	dynamodbMetastore appencryption.Metastore
	testContext       *dynamodbtest.DynamoDBTestContextV2
}

// Metastore is the SUT for the test suite.
func (suite *DynamoDBV2Suite) Metastore() appencryption.Metastore {
	return suite.dynamodbMetastore
}

func (suite *DynamoDBV2Suite) SetupSuite() {
	suite.instant = time.Now().Add(-24 * time.Hour).Unix()
	suite.testContext = dynamodbtest.NewDynamoDBTestContextV2(suite.T(), suite.instant)
}

func (suite *DynamoDBV2Suite) TearDownSuite() {
	suite.testContext.TearDownV2(suite.T())
}

func (suite *DynamoDBV2Suite) SetupTest() {
	suite.testContext.SeedDBV2(suite.T())
	suite.dynamodbMetastore = suite.testContext.NewMetastoreV2(false)
}

func (suite *DynamoDBV2Suite) TearDownTest() {
	suite.testContext.CleanDBV2(suite.T())
}

func TestDynamoDBV2Suite(t *testing.T) {
	if testing.Short() {
		t.Skip("too slow for testing.Short")
	}

	suite.Run(t, new(DynamoDBV2Suite))
}
