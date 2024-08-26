package integration_test

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/integrationtest/dynamodbtest"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

type metastoreFactory func(tc *dynamodbtest.DynamoDBTestContext, useRegionSuffix bool) appencryption.Metastore

func NewDynamoDBMetastoreFactory(tc *dynamodbtest.DynamoDBTestContext, useRegionSuffix bool) appencryption.Metastore {
	return tc.NewMetastore(persistence.WithDynamoDBRegionSuffix(useRegionSuffix))
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastore_EncryptDecrypt() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	// build a new metastore with regional suffixing enabled
	s.testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt(NewDynamoDBMetastoreFactory)
}

func (s *IntegrationTestSuite) testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt(f metastoreFactory) {
	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	testContext := dynamodbtest.NewDynamoDBTestContext(s.T(), instant)
	defer testContext.CleanDB(s.T())

	testContext.SeedDB(s.T())

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

		ctx := context.Background()

		drr, err = session.Encrypt(ctx, []byte(original))
		require.NoError(err)
		require.NotNil(drr)

		after, err := session.Decrypt(ctx, *drr)
		require.NoError(err)
		require.Equal(original, string(after))
	}()

	// now decrypt the DRR with a new session using the same (suffixed) partition
	session, err := factory.GetSession(partitionID)
	require.NoError(err)

	defer session.Close()

	after, err := session.Decrypt(context.Background(), *drr)
	require.NoError(err)

	require.Equal(original, string(after))
}

func (s *IntegrationTestSuite) TestSessionFactory_WithDynamoDBMetastore_EncryptDecrypt_RegionSuffixBackwardsCompatibility() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	s.testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt_RegionSuffixBackwardsCompatibility(NewDynamoDBMetastoreFactory)
}

func (s *IntegrationTestSuite) testSessionFactory_WithDynamoDBMetastore_EncryptDecrypt_RegionSuffixBackwardsCompatibility(f metastoreFactory) {
	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	testContext := dynamodbtest.NewDynamoDBTestContext(s.T(), instant)
	defer testContext.CleanDB(s.T())

	testContext.SeedDB(s.T())

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

		ctx := context.Background()

		drr, err = session.Encrypt(ctx, []byte(original))
		require.NoError(err)
		require.NotNil(drr)

		require.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), drr.Key.ParentKeyMeta.ID)

		after, err := session.Decrypt(ctx, *drr)
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
