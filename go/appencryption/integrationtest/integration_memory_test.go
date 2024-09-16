package integration_test

import (
	"context"
	"fmt"
	"testing"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

const original = "somesupersecretstring!hjdkashfjkdashfd"

// IntegrationTestSuite provides a suite of integration tests for the appencryption package.
// Tests center around session encrypt and decrypt operations using a SessionFactory with
// a static KMS and various metastore implementations (MemoryMetastore, DynamoDBMetastore).
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

func (s *IntegrationTestSuite) TestSessionFactory_WithMemoryMetastore_EncryptDecrypt() {
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

func (s *IntegrationTestSuite) TestSessionFactory_WithMemoryMetastore_Decrypt_WithMismatchPartition_ShouldFail() {
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

func TestIntegrationSuite(t *testing.T) {
	suite.Run(t, new(IntegrationTestSuite))
}
