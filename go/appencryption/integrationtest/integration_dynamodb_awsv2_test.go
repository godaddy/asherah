package integration_test

import (
	"context"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/integrationtest/dynamodbtest"
)

// metastoreV2Factory defines the function signature for creating AWS SDK v2 metastores.
type metastoreV2Factory func(tc *dynamodbtest.DynamoDBTestContextV2, useRegionSuffix bool) appencryption.Metastore

func NewDynamoDBMetastoreV2Factory(tc *dynamodbtest.DynamoDBTestContextV2, useRegionSuffix bool) appencryption.Metastore {
	return tc.NewMetastoreV2(useRegionSuffix)
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	// Use complete AWS SDK v2 test infrastructure
	s.testSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt(NewDynamoDBMetastoreV2Factory)
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt_RegionSuffixBackwardsCompatibility() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	s.testSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt_RegionSuffixBackwardsCompatibility(NewDynamoDBMetastoreV2Factory)
}

func (s *IntegrationTestSuite) testSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt(f metastoreV2Factory) {
	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	// Use complete AWS SDK v2 test context
	testContext := dynamodbtest.NewDynamoDBTestContextV2(s.T(), instant)
	defer testContext.TearDownV2(s.T())
	defer testContext.CleanDBV2(s.T())

	testContext.SeedDBV2(s.T())

	var drr *appencryption.DataRowRecord

	factory := appencryption.NewSessionFactory(
		&s.config,
		f(testContext, false),
		s.kms,
		s.c,
	)
	defer factory.Close()

	// encrypt and decrypt the DRR with a session and dispose of it asap
	func() {
		session, err := factory.GetSession(partitionID)
		require.NoError(err)
		defer session.Close()

		drr, err = session.Encrypt(context.Background(), []byte(original))
		require.NoError(err)
		require.NotNil(drr)
	}()

	// decrypt the DRR with a new session
	func() {
		session, err := factory.GetSession(partitionID)
		require.NoError(err)
		defer session.Close()

		actualData, err := session.Decrypt(context.Background(), *drr)
		require.NoError(err)
		require.Equal([]byte(original), actualData)
	}()
}

func (s *IntegrationTestSuite) testSessionFactory_WithDynamoDBMetastoreV2_EncryptDecrypt_RegionSuffixBackwardsCompatibility(f metastoreV2Factory) {
	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	// Use complete AWS SDK v2 test context
	testContext := dynamodbtest.NewDynamoDBTestContextV2(s.T(), instant)
	defer testContext.TearDownV2(s.T())
	defer testContext.CleanDBV2(s.T())

	testContext.SeedDBV2(s.T())

	var drr *appencryption.DataRowRecord

	// First, encrypt the original using a default, non-suffixed DynamoDBMetastore
	func() {
		factory := appencryption.NewSessionFactory(
			&s.config,
			f(testContext, false), // use non-suffixed metastore
			s.kms,
			s.c,
		)
		defer factory.Close()

		session, err := factory.GetSession(partitionID)
		require.NoError(err)
		defer session.Close()

		drr, err = session.Encrypt(context.Background(), []byte(original))
		require.NoError(err)
		require.NotNil(drr)

		// Verify we can decrypt with same session
		after, err := session.Decrypt(context.Background(), *drr)
		require.NoError(err)
		require.Equal(original, string(after))
	}()

	// Now decrypt the encrypted data via a new factory and session using a suffixed DynamoDBMetastore
	factory := appencryption.NewSessionFactory(
		&s.config,
		f(testContext, true), // use suffixed metastore
		s.kms,
		s.c,
	)
	defer factory.Close()

	session, err := factory.GetSession(partitionID)
	require.NoError(err)
	defer session.Close()

	after, err := session.Decrypt(context.Background(), *drr)
	require.NoError(err)
	require.Equal(original, string(after))
}
