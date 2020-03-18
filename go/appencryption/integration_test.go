package appencryption_test

import (
	"fmt"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

type IntegrationTestSuite struct {
	suite.Suite
	c         appencryption.AEAD
	config    appencryption.Config
	kms       *kms.StaticKMS
	metastore *persistence.MemoryMetastore
}

func (suite *IntegrationTestSuite) SetupTest() {
	suite.c = aead.NewAES256GCM()
	suite.config = appencryption.Config{
		Policy:  appencryption.NewCryptoPolicy(),
		Product: product,
		Service: service,
	}
	suite.metastore = persistence.NewMemoryMetastore()
}

func (suite *IntegrationTestSuite) TestIntegration() {
	var err error
	suite.kms, err = kms.NewStatic(staticKey, suite.c)

	assert.NoError(suite.T(), err)

	verify := assert.New(suite.T())

	factory := appencryption.NewSessionFactory(
		&suite.config,
		suite.metastore,
		suite.kms,
		suite.c,
	)
	session, _ := factory.GetSession(partitionID)

	dr, err := session.Encrypt([]byte("somesupersecretstring!hjdkashfjkdashfd"))
	if verify.NoError(err) && verify.NotNil(dr) {
		verify.Equal(fmt.Sprintf("_IK_%s_%s_%s", partitionID, service, product), dr.Key.ParentKeyMeta.ID)

		if after, err := session.Decrypt(*dr); verify.NoError(err) {
			verify.Equal("somesupersecretstring!hjdkashfjkdashfd", string(after))
		}
	}
}

func TestIntegrationSuite(t *testing.T) {
	suite.Run(t, new(IntegrationTestSuite))
}
