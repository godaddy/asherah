package kms_test

import (
	"context"
	"errors"
	"testing"

	"github.com/aws/aws-sdk-go-v2/aws"
	awskms "github.com/aws/aws-sdk-go-v2/service/kms"
	"github.com/aws/aws-sdk-go-v2/service/kms/types"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms"
)

var (
	preferred    = "us-east-1"
	preferredARN = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"

	secondary    = "us-west-2"
	secondaryARN = "arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012"

	regionArnMap = map[string]string{
		preferred: preferredARN,
		secondary: secondaryARN,
	}

	// fakeDataKey is the plaintext key returned by our mocked AWSClient.GenerateDataKey
	fakeDataKey          = []byte("plaintext")
	fakeDataKeyEncrypted = []byte("encrypted")

	// fakeCipherText is the ciphertext returned by our mocked crypto.Encrypt
	fakeCipherText = []byte("ciphertext")

	// envelopeJSON contains an encrypted key and its KMS KEKs.
	// encrypted* values are base64 encoded
	envelopeJSON = []byte(`{
        "encryptedKey":"Y2lwaGVydGV4dA==",
        "kmsKeks":[
            {
                "region":"us-east-1",
                "arn":"arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
                "encryptedKek":"ZW5jcnlwdGVk"
            },
            {
                "region":"us-west-2",
                "arn":"arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012",
                "encryptedKek":"ZW5jcnlwdGVk"
            }
        ]
    }`)
)

func TestNewAWS(t *testing.T) {
	keyManagement, err := kms.NewAWS(&MockCrypto{}, preferred, regionArnMap)
	require.NoError(t, err)
	require.NotNil(t, keyManagement)
}

func TestAWSKMS_Encrypt(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}
	crypto.On("Encrypt", keyBytes, fakeDataKey).Return(fakeCipherText, nil)

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			// GenerateDataKey is called by AWSKMS.generateDataKey which iterates over all clients
			// and calls GenerateDataKey on each one, returning the first successful result.
			client.On("GenerateDataKey", mock.Anything, mock.Anything, mock.Anything).
				Return(&awskms.GenerateDataKeyOutput{
					KeyId:          &preferredARN,
					Plaintext:      fakeDataKey,
					CiphertextBlob: fakeDataKeyEncrypted,
				}, nil).Once()
		case secondary:
			// Encrypt is called by AWSKMS.encryptRegionalKEKs which iterates over all clients and calls Encrypt on each
			// one (skipping the region that generated the data key). In this case, the data key is generated in the
			// preferred region, so Encrypt is called for the secondary region.
			client.On("Encrypt", mock.Anything, &awskms.EncryptInput{
				KeyId:     &secondaryARN,
				Plaintext: fakeDataKey,
			}, mock.Anything).Return(&awskms.EncryptOutput{
				KeyId:          &secondaryARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, nil).Once()
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test EncryptKey
	envelope, err := kms.EncryptKey(context.Background(), keyBytes)
	require.NoError(t, err)
	require.NotNil(t, envelope)

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func assertMockClientCalls(t *testing.T, clients map[string]*MockClient, expectedNumClients int) {
	require.Len(t, clients, expectedNumClients)

	for _, c := range clients {
		c.AssertExpectations(t)
	}
}

func TestAWSKMS_Encrypt_WithEncryptKeyFailure_AllRegions(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}

	// Return an error for the Encrypt call to test the error handling
	crypto.On("Encrypt", keyBytes, fakeDataKey).Return(nil, errors.New("forced error for test"))

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			// GenerateDataKey is called by AWSKMS.generateDataKey which iterates over all clients
			// and calls GenerateDataKey on each one, returning the first successful result.
			client.On("GenerateDataKey", mock.Anything, mock.Anything, mock.Anything).
				Return(&awskms.GenerateDataKeyOutput{
					KeyId:          &preferredARN,
					Plaintext:      fakeDataKey,
					CiphertextBlob: fakeDataKeyEncrypted,
				}, nil).Once()
		case secondary:
			// no secondary region calls are expected due to the crypto.Encrypt failure in the preferred region
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test EncryptKey
	_, err = kms.EncryptKey(context.Background(), keyBytes)
	require.ErrorContains(t, err, "error encrypting key")

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Encrypt_WithPrimaryFailure_GenerateDataKey_FallbackSuccess(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}
	crypto.On("Encrypt", keyBytes, fakeDataKey).Return(fakeCipherText, nil)

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			// GenerateDataKey is called by AWSKMS.generateDataKey which iterates over all clients
			// and calls GenerateDataKey on each one, returning the first successful result.
			// We fail the first call to GenerateDataKey to test the fallback to the next region.
			client.On("GenerateDataKey", mock.Anything, &awskms.GenerateDataKeyInput{
				KeyId:   &preferredARN,
				KeySpec: types.DataKeySpecAes256,
			}, mock.Anything).Return(nil, errors.New("forced error for test")).Once()

			// Encrypt is called by AWSKMS.encryptRegionalKEKs which iterates over all clients and calls Encrypt on each one.
			// Note that this operation would normally be unnecessary for the preferred region as the data key is generated
			// in that region and its ciphertext is used directly. However, the Encrypt operation is still called in
			// this case due to the forced error in GenerateDataKey above (causing the fallback to the secondary region).
			client.On("Encrypt", mock.Anything, &awskms.EncryptInput{
				KeyId:     &preferredARN,
				Plaintext: fakeDataKey,
			}, mock.Anything).Return(&awskms.EncryptOutput{
				KeyId:          &preferredARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, nil).Once()
		case secondary:
			// The second call to GenerateDataKey should succeed.
			client.On("GenerateDataKey", mock.Anything, mock.Anything, mock.Anything).
				Return(&awskms.GenerateDataKeyOutput{
					KeyId:          &secondaryARN,
					Plaintext:      fakeDataKey,
					CiphertextBlob: fakeDataKeyEncrypted,
				}, nil).Once()

			// Note that Encrypt is not called for the secondary region as the generated data key's ciphertext is used
			// directly.
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test EncryptKey
	envelope, err := kms.EncryptKey(context.Background(), keyBytes)
	require.NoError(t, err)
	require.NotNil(t, envelope)

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Encrypt_WithSecondaryEncryptKeyFailure_CallSucceeds(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}
	crypto.On("Encrypt", keyBytes, fakeDataKey).Return(fakeCipherText, nil)

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			// GenerateDataKey is called by AWSKMS.generateDataKey which iterates over all clients
			// and calls GenerateDataKey on each one, returning the first successful result.
			client.On("GenerateDataKey", mock.Anything, &awskms.GenerateDataKeyInput{
				KeyId:   &preferredARN,
				KeySpec: types.DataKeySpecAes256,
			}, mock.Anything).Return(&awskms.GenerateDataKeyOutput{
				KeyId:          &preferredARN,
				Plaintext:      fakeDataKey,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, nil).Once()

			// The ciphertext is used directly from the generated data key, so there is no call to Encrypt for the
			// preferred region.
		case secondary:
			// GenerateDataKey is not called for the secondary region.

			// Fail the Encrypt call for the secondary region, but the system should still succeed as long as the
			// at least one region succeeds.
			client.On("Encrypt", mock.Anything, &awskms.EncryptInput{
				KeyId:     &secondaryARN,
				Plaintext: fakeDataKey,
			}, mock.Anything).Return(nil, errors.New("secondary region goes boom!")).Once()
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test EncryptKey
	envelope, err := kms.EncryptKey(context.Background(), keyBytes)
	require.NoError(t, err)
	require.NotNil(t, envelope)

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Encrypt_WithGenerateDataKeyFailure_AllRegionsFail(t *testing.T) {
	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		// GenerateDataKey is called by AWSKMS.generateDataKey which iterates over all clients
		// and calls GenerateDataKey on each one, returning the first successful result.
		// Fail all calls to test the error handling.
		client.On("GenerateDataKey", mock.Anything, mock.Anything, mock.Anything).Return(nil, errors.New("forced error for test")).Once()

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(&MockCrypto{}, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	_, err = kms.EncryptKey(context.Background(), []byte("test"))
	require.ErrorContains(t, err, "all regions returned errors")

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Decrypt(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}
	crypto.On("Decrypt", fakeCipherText, fakeDataKey).Return(keyBytes, nil)

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			client.On("Decrypt", mock.Anything, &awskms.DecryptInput{
				KeyId:          &preferredARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, mock.Anything).Return(&awskms.DecryptOutput{
				Plaintext: fakeDataKey,
			}, nil).Once()
		default:
			// secondary region(s) has no expected calls
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test DecryptKey
	envelope, err := kms.DecryptKey(context.Background(), envelopeJSON)
	require.NoError(t, err)
	require.NotNil(t, envelope)

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Decrypt_WithFallback(t *testing.T) {
	// keyBytes is the plaintext key to be encrypted by the system under test (AWSKMS.EncryptKey)
	keyBytes := []byte("test")

	crypto := &MockCrypto{}
	crypto.On("Decrypt", fakeCipherText, fakeDataKey).Return(keyBytes, nil)

	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			client.On("Decrypt", mock.Anything, &awskms.DecryptInput{
				KeyId:          &preferredARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, mock.Anything).Return(nil, errors.New("forced error for test")).Once()
		case secondary:
			client.On("Decrypt", mock.Anything, &awskms.DecryptInput{
				KeyId:          &secondaryARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, mock.Anything).Return(&awskms.DecryptOutput{
				Plaintext: fakeDataKey,
			}, nil).Once()
		}

		mockClients[cfg.Region] = client

		return client
	}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	// Test DecryptKey
	envelope, err := kms.DecryptKey(context.Background(), envelopeJSON)
	require.NoError(t, err)
	require.NotNil(t, envelope)

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 2)
}

func TestAWSKMS_Decrypt_FailAllRegions(t *testing.T) {
	// mockClients is populated with the mocked AWSClient instances for each region (see factory below)
	mockClients := make(map[string]*MockClient)

	arnMap := make(map[string]string, len(regionArnMap)+1)
	for region, arn := range regionArnMap {
		arnMap[region] = arn
	}

	// Add a region that doesn't exist in envelopeJSON KMS KEKs
	arnMap["eu-west-1"] = "arn:aws:kms:eu-west-1:123456789012:key/12345678-1234-1234-1234-123456789012"

	factory := func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		client := &MockClient{}

		switch cfg.Region {
		case preferred:
			// decrypt call for the preferred region will succeed, but the crypto.Decrypt call will fail
			client.On("Decrypt", mock.Anything, &awskms.DecryptInput{
				KeyId:          &preferredARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, mock.Anything).Return(&awskms.DecryptOutput{
				Plaintext: fakeDataKey,
			}, nil).Once()
		case secondary:
			// fail the decrypt call for the secondary region
			client.On("Decrypt", mock.Anything, &awskms.DecryptInput{
				KeyId:          &secondaryARN,
				CiphertextBlob: fakeDataKeyEncrypted,
			}, mock.Anything).Return(nil, errors.New("forced error for test (secondary AWSClient.Decrypt)")).Once()
		}

		mockClients[cfg.Region] = client

		return client
	}

	crypto := &MockCrypto{}
	crypto.On("Decrypt", fakeCipherText, fakeDataKey).Return(nil, errors.New("forced error for test (crypto.Decrypt)"))

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, arnMap).
		WithPreferredRegion(preferred).
		WithKMSFactory(factory).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	_, err = kms.DecryptKey(context.Background(), envelopeJSON)
	require.ErrorContains(t, err, "decrypt failed in all regions")

	// Ensure we have the expected client instances
	assertMockClientCalls(t, mockClients, 3)
}

func TestAWSKMS_Decrypt_WithInvalidEnvelope(t *testing.T) {
	crypto := &MockCrypto{}

	// Create a new AWSKMS instance using the builder
	kms, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		Build()
	require.NoError(t, err)
	require.NotNil(t, kms)

	_, err = kms.DecryptKey(context.Background(), []byte("invalid"))
	require.ErrorContains(t, err, "unable to unmarshal envelope")
}
