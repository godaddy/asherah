package integration_test

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"

	"github.com/godaddy/asherah/go/appencryption/integrationtest/persistence/persistencetest"
)

const original = "somesupersecretstring!hjdkashfjkdashfd"

type IntegrationTestSuite struct {
	suite.Suite
	c      appencryption.AEAD
	config appencryption.Config
	kms    *kms.StaticKMS
}

func (s *IntegrationTestSuite) SetupTest() {
	s.c = aead.NewAES256GCM()
	s.config = appencryption.Config{
		Policy:  appencryption.NewCryptoPolicy(),
		Product: product,
		Service: service,
	}

	var err error
	s.kms, err = kms.NewStatic(staticKey, s.c)
	s.NoError(err)
}

func (s *IntegrationTestSuite) TestIntegration() {
	metastore := persistence.NewMemoryMetastore()

	factory := appencryption.NewSessionFactory(
		&s.config,
		metastore,
		s.kms,
		s.c,
	)
	defer factory.Close()

	session, _ := factory.GetSession(partitionID)
	defer session.Close()

	ctx := context.Background()

	dr, err := session.Encrypt(ctx, []byte(original))
	if s.NoError(err) && s.NotNil(dr) {
		s.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), dr.Key.ParentKeyMeta.ID)

		if after, err := session.Decrypt(ctx, *dr); s.NoError(err) {
			s.Equal(original, string(after))
		}
	}
}

func (s *IntegrationTestSuite) TestCrossPartitionDecryptShouldFail() {
	metastore := persistence.NewMemoryMetastore()
	require := s.Require()

	factory := appencryption.NewSessionFactory(
		&s.config,
		metastore,
		s.kms,
		s.c,
	)
	defer factory.Close()

	session, err := factory.GetSession(partitionID)
	require.NoError(err)

	defer session.Close()

	ctx := context.Background()

	dr, err := session.Encrypt(ctx, []byte(original))
	require.NoError(err)
	require.NotNil(dr)

	after, err := session.Decrypt(ctx, *dr)
	require.NoError(err)
	require.Equal(original, string(after), "decrypted value does not match the original")

	// Now create a new session using a different partition ID and veriry
	// the new session is unable to decrypt the other session's DRR.
	altPartition := partitionID + "alt"
	altSession, err := factory.GetSession(altPartition)
	require.NoError(err)

	defer altSession.Close()

	_, err = altSession.Decrypt(ctx, *dr)
	require.Error(err, "decrypt expected to return error")
}

func (s *IntegrationTestSuite) TestDynamoDBRegionSuffixBackwardsCompatibility() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	testContext := persistencetest.NewDynamoDBTestContext(s.T(), instant)
	defer testContext.CleanDB(s.T())

	testContext.SeedDB(s.T())

	var drr *appencryption.DataRowRecord

	// First, encrypt the original using a default, non-suffixed DynamoDBMetastore
	func() {
		factory := appencryption.NewSessionFactory(
			&s.config,
			testContext.NewMetastore(),
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
		testContext.NewMetastore(persistence.WithDynamoDBRegionSuffix(true)),
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

func (s *IntegrationTestSuite) TestDynamoDBRegionSuffix() {
	if testing.Short() {
		s.T().Skip("too slow for testing.Short")
	}

	instant := time.Now().Add(-24 * time.Hour).Unix()
	require := s.Require()

	testContext := persistencetest.NewDynamoDBTestContext(s.T(), instant)
	defer testContext.CleanDB(s.T())

	testContext.SeedDB(s.T())

	var drr *appencryption.DataRowRecord

	// build a new metastore with regional suffixing enabled
	metastore := testContext.NewMetastore(persistence.WithDynamoDBRegionSuffix(true))

	factory := appencryption.NewSessionFactory(
		&s.config,
		metastore,
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

func TestIntegrationSuite(t *testing.T) {
	suite.Run(t, new(IntegrationTestSuite))
}
