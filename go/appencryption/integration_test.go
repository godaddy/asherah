package appencryption_test

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence/persistencetest"
)

const original = "somesupersecretstring!hjdkashfjkdashfd"

type IntegrationTestSuite struct {
	suite.Suite
	c      appencryption.AEAD
	config appencryption.Config
	kms    *kms.StaticKMS
}

func (suite *IntegrationTestSuite) SetupTest() {
	suite.c = aead.NewAES256GCM()
	suite.config = appencryption.Config{
		Policy:  appencryption.NewCryptoPolicy(),
		Product: product,
		Service: service,
	}

	var err error
	suite.kms, err = kms.NewStatic(staticKey, suite.c)
	assert.NoError(suite.T(), err)
}

func (suite *IntegrationTestSuite) TestIntegration() {
	metastore := persistence.NewMemoryMetastore()
	verify := assert.New(suite.T())

	factory := appencryption.NewSessionFactory(
		&suite.config,
		metastore,
		suite.kms,
		suite.c,
	)
	defer factory.Close()

	session, _ := factory.GetSession(partitionID)
	defer session.Close()

	ctx := context.Background()

	dr, err := session.Encrypt(ctx, []byte(original))
	if verify.NoError(err) && verify.NotNil(dr) {
		verify.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), dr.Key.ParentKeyMeta.ID)

		if after, err := session.Decrypt(ctx, *dr); verify.NoError(err) {
			verify.Equal(original, string(after))
		}
	}
}

func (suite *IntegrationTestSuite) TestCrossPartitionDecryptShouldFail() {
	metastore := persistence.NewMemoryMetastore()
	verify := assert.New(suite.T())
	must := require.New(suite.T())

	factory := appencryption.NewSessionFactory(
		&suite.config,
		metastore,
		suite.kms,
		suite.c,
	)
	defer factory.Close()

	session, err := factory.GetSession(partitionID)
	must.NoError(err)

	defer session.Close()

	ctx := context.Background()

	dr, err := session.Encrypt(ctx, []byte(original))
	must.NoError(err)
	must.NotNil(dr)

	after, err := session.Decrypt(ctx, *dr)
	must.NoError(err)
	verify.Equal(original, string(after), "decrypted value does not match the original")

	// Now create a new session using a different partition ID and veriry
	// the new session is unable to decrypt the other session's DRR.
	altPartition := partitionID + "alt"
	altSession, err := factory.GetSession(altPartition)
	must.NoError(err)

	defer altSession.Close()

	_, err = altSession.Decrypt(ctx, *dr)
	must.Error(err, "decrypt expected to return error")
}

func (suite *IntegrationTestSuite) TestDynamoDBRegionSuffixBackwardsCompatibility() {
	if testing.Short() {
		suite.T().Skip("too slow for testing.Short")
	}

	instant := time.Now().Add(-24 * time.Hour).Unix()
	verify := assert.New(suite.T())

	testContext := persistencetest.NewDynamoDBTestContext(instant)
	defer testContext.CleanDB()

	testContext.SeedDB()

	var drr *appencryption.DataRowRecord

	// First, encrypt the original using a default, non-suffixed DynamoDBMetastore
	func() {
		factory := appencryption.NewSessionFactory(
			&suite.config,
			testContext.NewMetastore(),
			suite.kms,
			suite.c,
		)
		defer factory.Close()

		session, err := factory.GetSession(partitionID)
		verify.NoError(err)

		defer session.Close()

		ctx := context.Background()

		drr, err = session.Encrypt(ctx, []byte(original))
		if verify.NoError(err) && verify.NotNil(drr) {
			verify.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), drr.Key.ParentKeyMeta.ID)

			if after, err := session.Decrypt(ctx, *drr); verify.NoError(err) {
				verify.Equal(original, string(after))
			}
		}
	}()

	// Now decrypt the encrypted data via a new factory and session using a suffixed DynamoDBMetastore
	factory := appencryption.NewSessionFactory(
		&suite.config,
		testContext.NewMetastore(persistence.WithDynamoDBRegionSuffix(true)),
		suite.kms,
		suite.c,
	)
	defer factory.Close()

	session, err := factory.GetSession(partitionID)
	verify.NoError(err)

	defer session.Close()

	if after, err := session.Decrypt(context.Background(), *drr); verify.NoError(err) {
		verify.Equal(original, string(after))
	}
}

func (suite *IntegrationTestSuite) TestDynamoDBRegionSuffix() {
	if testing.Short() {
		suite.T().Skip("too slow for testing.Short")
	}

	instant := time.Now().Add(-24 * time.Hour).Unix()
	verify := assert.New(suite.T())

	testContext := persistencetest.NewDynamoDBTestContext(instant)
	defer testContext.CleanDB()

	testContext.SeedDB()

	var drr *appencryption.DataRowRecord

	// build a new metastore with regional suffixing enabled
	metastore := testContext.NewMetastore(persistence.WithDynamoDBRegionSuffix(true))

	factory := appencryption.NewSessionFactory(
		&suite.config,
		metastore,
		suite.kms,
		suite.c,
	)
	defer factory.Close()

	// encrypt and decrypt the DRR with a session and dispose of it asap
	func() {
		session, err := factory.GetSession(partitionID)
		verify.NoError(err)

		defer session.Close()

		ctx := context.Background()

		drr, err = session.Encrypt(ctx, []byte(original))
		if verify.NoError(err) && verify.NotNil(drr) {
			if after, err := session.Decrypt(ctx, *drr); verify.NoError(err) {
				verify.Equal(original, string(after))
			}
		}
	}()

	// now decrypt the DRR with a new session using the same (suffixed) partition
	session, err := factory.GetSession(partitionID)
	verify.NoError(err)

	defer session.Close()

	if after, err := session.Decrypt(context.Background(), *drr); verify.NoError(err) {
		verify.Equal(original, string(after))
	}
}

func TestIntegrationSuite(t *testing.T) {
	suite.Run(t, new(IntegrationTestSuite))
}
