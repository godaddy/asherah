package appencryption_test

import (
	"fmt"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence/persistencetest"
)

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

	original := "somesupersecretstring!hjdkashfjkdashfd"

	dr, err := session.Encrypt([]byte(original))
	if verify.NoError(err) && verify.NotNil(dr) {
		verify.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), dr.Key.ParentKeyMeta.ID)

		if after, err := session.Decrypt(*dr); verify.NoError(err) {
			verify.Equal(original, string(after))
		}
	}
}

func (suite *IntegrationTestSuite) TestDynamoDBRegionSuffix() {
	instant := time.Now().Add(-24 * time.Hour).Unix()
	original := "somesupersecretstring!hjdkashfjkdashfd"
	verify := assert.New(suite.T())

	testContext := persistencetest.NewDynamoDBTestContext(instant)
	defer testContext.TearDown()

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

		drr, err = session.Encrypt([]byte(original))
		if verify.NoError(err) && verify.NotNil(drr) {
			verify.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), drr.Key.ParentKeyMeta.ID)

			if after, err := session.Decrypt(*drr); verify.NoError(err) {
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

	if after, err := session.Decrypt(*drr); verify.NoError(err) {
		verify.Equal(original, string(after))
	}
}

func TestIntegrationSuite(t *testing.T) {
	suite.Run(t, new(IntegrationTestSuite))
}
