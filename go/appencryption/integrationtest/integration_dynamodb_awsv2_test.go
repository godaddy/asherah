package integration_test

import (
	"testing"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/integrationtest/dynamodbtest"
)

func NewDynamoDBMetastoreV2Factory(tc *dynamodbtest.DynamoDBTestContext, useRegionSuffix bool) appencryption.Metastore {
	return tc.NewMetastoreV2(useRegionSuffix)
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	// build a new metastore with regional suffixing enabled
	s.testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt(NewDynamoDBMetastoreV2Factory)
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt_RegionSuffixBackwardsCompatibility() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	s.testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt_RegionSuffixBackwardsCompatibility(NewDynamoDBMetastoreV2Factory)
}
