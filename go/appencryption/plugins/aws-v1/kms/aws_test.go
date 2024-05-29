package kms

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/aws/aws-sdk-go/aws/request"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/aws/aws-sdk-go/service/kms"
	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"

	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
)

var (
	// nolint:lll
	keyJSON = `{
		"kmsKeks": [
		  {
			"region": "us-west-2",
			"arn": "9b3bedaf-9767-4712-87f9-88befbe0b5c9",
			"encryptedKek": "AQICAHhKCgIzMuTVw/yj2k2mGhpHZJLmI5wUT5P9ZubwmhvkmwH1fGaWfhHEtdofxQogk3RNAAAAfjB8BgkqhkiG9w0BBwagbzBtAgEAMGgGCSqGSIb3DQEHATAeBglghkgBZQMEAS4wEQQMCZgoOYlNTrqC5zXRAgEQgDsLzVF4aCnEY3pp8zCbRhqtYTdtoalRLEMWLATr0P7j3NnIq8nh6IppvF7kMKT8Y1wtIcGAeIzmHHvvfA=="
		  },
		  {
			"region": "us-east-2",
			"arn": "a7f3c918-8068-4dc5-b323-86377fe70fbe",
			"encryptedKek": "AQICAHiGy//xbN3HLZk9GeEmF1B03AEp2W0c1/SR2e7CK+tFKwFrE80QJ6JNmg4q7H9dLRDnAAAAfjB8BgkqhkiG9w0BBwagbzBtAgEAMGgGCSqGSIb3DQEHATAeBglghkgBZQMEAS4wEQQMjH2F1q3kUmtrhvVfAgEQgDsIsFZdB4DEUKcwqOEtGAkVehHdl8a9vpcAFknm5mql5PRihVLWYQL1c9Hw1cjSIVYy/tlaMPTecLfGUA=="
		  }
		],
		"encryptedKey": "+FEJWwhJA3FDFY0rMjZ9/dLqfW4pbrLZ6b4UdlWCQd2TGQ2slCdVRtBDwQm4vtWCrYiOJV6nmcPrxqjI"
	  }`

	secretFactory = new(memguard.SecretFactory)

	// us-west-2 is the preferred region for our tests
	preferredRegion     = "us-west-2"
	preferredRegionARN  = "some:arn:value"
	usEast2             = "us-east-2"
	usEast2ARN          = "some:arn:value:east"
	invalidRegion       = "us-east-34"
	genericErrorMessage = "generic error message"
	keySpec             = "AES_256"

	plaintextKey = []byte("plaintextKey")
	decryptedKey = []byte("decryptedKey")
	encryptedKey = []byte("encryptedKey")
	encryptedKEK = []byte("encryptedKek")
	randomBytes  = []byte("testing")

	regionArnMap = map[string]string{
		preferredRegion: preferredRegionARN,
		usEast2:         usEast2ARN,
	}
)

type MockCrypto struct {
	mock.Mock
}

func (c *MockCrypto) NonceByteSize() int {
	ret := c.Called()

	var nonceSize int
	if b := ret.Get(0); b != nil {
		nonceSize = b.(int)
	}

	return nonceSize
}

func (c *MockCrypto) KeyByteSize() int {
	ret := c.Called()

	var keySize int
	if b := ret.Get(0); b != nil {
		keySize = b.(int)
	}

	return keySize
}

func (c *MockCrypto) Encrypt(data, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (c *MockCrypto) Decrypt(data []byte, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

type MockClient struct {
	mock.Mock
}

func (c *MockClient) EncryptWithContext(ctx context.Context, input *kms.EncryptInput, opts ...request.Option) (*kms.EncryptOutput, error) {
	args := c.Called(ctx, input, opts)

	if out := args.Get(0); out != nil {
		return out.(*kms.EncryptOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) GenerateDataKeyWithContext(ctx context.Context, input *kms.GenerateDataKeyInput, opts ...request.Option) (*kms.GenerateDataKeyOutput, error) {
	args := c.Called(ctx, input, opts)

	if out := args.Get(0); out != nil {
		return out.(*kms.GenerateDataKeyOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) DecryptWithContext(ctx context.Context, input *kms.DecryptInput, opts ...request.Option) (*kms.DecryptOutput, error) {
	args := c.Called(ctx, input, opts)

	if out := args.Get(0); out != nil {
		return out.(*kms.DecryptOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func TestAWSKMS_NewAWS(t *testing.T) {
	crypto := aead.NewAES256GCM()
	m, err := NewAWS(crypto, preferredRegion, regionArnMap)
	assert.NoError(t, err)

	assert.NotNil(t, m)
	assert.Len(t, m.Clients, 2)
}

type MockARNMap struct {
	mock.Mock
}

func (m *MockARNMap) createAWSKMSClients() ([]AWSKMSClient, error) {
	ret := m.Called()

	if clients := ret.Get(0); clients != nil {
		return clients.([]AWSKMSClient), ret.Error(1)
	}

	return nil, ret.Error(1)
}

func TestAWSKMS_NewAWSError(t *testing.T) {
	crypto := aead.NewAES256GCM()
	mapper := new(MockARNMap)

	mapper.On("createAWSKMSClients").Return(nil, errors.New("boom"))

	k, err := newAWS(crypto, preferredRegion, mapper)
	assert.EqualError(t, err, "boom")
	assert.Nil(t, k)
}

func TestAWSKMS_NewAWSKMSClient(t *testing.T) {
	sess, _ := session.NewSession()
	client := newAWSKMSClient(sess, preferredRegion, preferredRegionARN)

	assert.NotNil(t, client)
	assert.Equal(t, preferredRegion, client.Region)
	assert.Equal(t, preferredRegionARN, client.ARN)
}

func TestAWSKMS_CreateAWSKMSClients(t *testing.T) {
	clients, err := createAWSKMSClients(regionArnMap)
	assert.NoError(t, err)

	assert.NotNil(t, clients)
	assert.Len(t, clients, 2)
}

func TestAWSKMS_SortClients(t *testing.T) {
	preferredClient := new(MockClient)
	usEast2Client := new(MockClient)

	clients := []AWSKMSClient{
		{
			KMS:    usEast2Client,
			Region: usEast2,
			ARN:    usEast2ARN,
		},
		{
			KMS:    preferredClient,
			Region: preferredRegion,
			ARN:    preferredRegionARN,
		},
	}
	clients = sortClients(preferredRegion, clients)

	assert.Equal(t, clients[0].Region, preferredRegion)
}

func TestAWSKMS_DecryptKey(t *testing.T) {
	preferredClient := new(MockClient)
	// Decryption with the first (preferred) region fails
	preferredClient.On("DecryptWithContext", mock.Anything, mock.Anything, mock.Anything).Return(
		nil, errors.New(genericErrorMessage))

	usEast2Client := new(MockClient)
	decryptInput := &kms.DecryptInput{
		CiphertextBlob: encryptedKEK,
	}
	// Decryption with the second region is successful
	usEast2Client.On("DecryptWithContext", mock.Anything, decryptInput, mock.Anything).Return(
		&kms.DecryptOutput{
			Plaintext: plaintextKey,
		}, nil)

	crypt := new(MockCrypto)
	crypt.On("Decrypt", encryptedKey, plaintextKey).Return(decryptedKey, nil)

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    preferredClient,
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
			{
				KMS:    usEast2Client,
				Region: usEast2,
				ARN:    usEast2ARN,
			},
		},
	}

	en := envelope{
		EncryptedKey: encryptedKey,
		KMSKEKs: keys{
			{
				EncryptedKEK: encryptedKEK,
				Region:       preferredRegion,
				ARN:          preferredRegionARN,
			},
			{
				EncryptedKEK: encryptedKEK,
				Region:       usEast2,
				ARN:          usEast2ARN,
			},
		},
	}

	enBytes, err := json.Marshal(en)

	if assert.NoError(t, err) {
		k, err := m.DecryptKey(context.Background(), enBytes)
		if assert.NoError(t, err) && assert.NotNil(t, k) {
			assert.Equal(t, decryptedKey, k)
		}
	}
}

func TestAWSKMS_DecryptKey_ReturnsErrorIfIfEnvelopeIsInvalid(t *testing.T) {
	m, err := NewAWS(aead.NewAES256GCM(), preferredRegion, regionArnMap)
	assert.NoError(t, err)

	decryptedBytes, err := m.DecryptKey(context.Background(), []byte("`"))
	assert.Error(t, err)
	assert.Nil(t, decryptedBytes)
}

func TestAWSKMS_DecryptKey_ReturnsErrorIfKMSDecryptFails(t *testing.T) {
	preferredClient := new(MockClient)

	preferredClient.On("DecryptWithContext", mock.Anything, mock.Anything, mock.Anything).
		Return(nil, errors.New(genericErrorMessage))

	crypt := new(MockCrypto)

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    preferredClient,
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
		},
	}

	en := envelope{
		EncryptedKey: encryptedKey,
		KMSKEKs: keys{
			{
				EncryptedKEK: encryptedKEK,
				Region:       preferredRegion,
				ARN:          preferredRegionARN,
			},
			{
				EncryptedKEK: encryptedKEK,
				Region:       usEast2,
				ARN:          usEast2ARN,
			},
		},
	}

	enBytes, _ := json.Marshal(en)
	decBytes, er := m.DecryptKey(context.Background(), enBytes)

	assert.Error(t, er)
	assert.Nil(t, decBytes)
}

func TestAWSKMS_DecryptKey_ReturnsErrorIfDEKDecryptFails(t *testing.T) {
	preferredClient := new(MockClient)
	decryptInput := &kms.DecryptInput{
		CiphertextBlob: encryptedKEK,
	}
	preferredClient.On("DecryptWithContext", mock.Anything, decryptInput, mock.Anything).Return(
		&kms.DecryptOutput{
			Plaintext: plaintextKey,
		}, nil)

	crypt := new(MockCrypto)
	crypt.On("Decrypt", mock.AnythingOfType("[]uint8"), mock.AnythingOfType("[]uint8")).
		Return(nil, errors.New(genericErrorMessage))

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    preferredClient,
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
		},
	}

	en := envelope{
		EncryptedKey: encryptedKey,
		KMSKEKs: keys{
			{
				EncryptedKEK: encryptedKEK,
				Region:       preferredRegion,
				ARN:          preferredRegionARN,
			},
			{
				EncryptedKEK: encryptedKEK,
				Region:       usEast2,
				ARN:          usEast2ARN,
			},
		},
	}

	enBytes, _ := json.Marshal(en)
	decBytes, er := m.DecryptKey(context.Background(), enBytes)

	assert.Error(t, er)
	assert.Nil(t, decBytes)
}

func TestAWSKMS_EncryptKey(t *testing.T) {
	generateDataKeyFunc = func(ctx context.Context, clients []AWSKMSClient) (output *kms.GenerateDataKeyOutput, e error) {
		return &kms.GenerateDataKeyOutput{
			CiphertextBlob: encryptedKey,
			KeyId:          &preferredRegionARN,
			Plaintext:      plaintextKey,
		}, nil
	}

	defer func() {
		generateDataKeyFunc = generateDataKey
	}()

	encryptAllRegionsFunc = func(ctx context.Context, resp *kms.GenerateDataKeyOutput, clients []AWSKMSClient) <-chan encryptionKey {
		results := make(chan encryptionKey, 2)
		defer close(results)
		results <- encryptionKey{
			Region:       preferredRegion,
			ARN:          preferredRegionARN,
			EncryptedKEK: encryptedKEK,
		}
		results <- encryptionKey{
			Region:       usEast2,
			ARN:          usEast2ARN,
			EncryptedKEK: encryptedKEK,
		}

		return results
	}

	defer func() {
		encryptAllRegionsFunc = encryptAllRegions
	}()

	crypt := new(MockCrypto)
	crypt.On("Encrypt", randomBytes, plaintextKey).Return(encryptedKey, nil)

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    new(MockClient),
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
			{
				KMS:    new(MockClient),
				Region: usEast2,
				ARN:    usEast2ARN,
			},
		},
	}

	encKey, err := m.EncryptKey(context.Background(), randomBytes)

	assert.NoError(t, err)
	assert.NotNil(t, encKey)
	// Check that the plaintextKey is getting wiped
	assert.Equal(t, make([]byte, len(plaintextKey)), plaintextKey)

	kek := new(envelope)
	if assert.NoError(t, json.Unmarshal(encKey, kek)) {
		assert.Len(t, kek.KMSKEKs, 2)
		assert.Equal(t, kek.EncryptedKey, encryptedKey)
	}
}

func TestAWSKMS_EncryptKey_ReturnsErrorIfGenerateDataKeyFails(t *testing.T) {
	defer func() {
		generateDataKeyFunc = generateDataKey
	}()

	generateDataKeyFunc = func(ctx context.Context, clients []AWSKMSClient) (output *kms.GenerateDataKeyOutput, e error) {
		return nil, errors.New(genericErrorMessage)
	}

	preferredClient := new(MockClient)
	crypt := new(MockCrypto)

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    preferredClient,
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
		},
	}

	_, err := m.EncryptKey(context.Background(), randomBytes)

	assert.Error(t, err)
}

func TestAWSKMS_EncryptKey_ReturnsErrorIfEncryptKEKFails(t *testing.T) {
	defer func() {
		generateDataKeyFunc = generateDataKey
	}()

	generateDataKeyFunc = func(ctx context.Context, clients []AWSKMSClient) (output *kms.GenerateDataKeyOutput, e error) {
		return &kms.GenerateDataKeyOutput{
			CiphertextBlob: encryptedKey,
			KeyId:          &usEast2ARN,
			Plaintext:      plaintextKey,
		}, nil
	}

	preferredClient := new(MockClient)

	crypt := new(MockCrypto)
	crypt.On("Encrypt", randomBytes, plaintextKey).Return(nil, errors.New(genericErrorMessage))

	m := AWSKMS{
		Crypto: crypt,
		Clients: []AWSKMSClient{
			{
				KMS:    preferredClient,
				Region: preferredRegion,
				ARN:    preferredRegionARN,
			},
		},
	}

	_, err := m.EncryptKey(context.Background(), randomBytes)

	assert.Error(t, err)
}

func TestAWSKMS_EncryptAllRegions(t *testing.T) {
	preferredClient := new(MockClient)

	dataKey := &kms.GenerateDataKeyOutput{
		CiphertextBlob: encryptedKey,
		KeyId:          &preferredRegionARN,
		Plaintext:      plaintextKey,
	}

	input := &kms.EncryptInput{
		KeyId:     &usEast2ARN,
		Plaintext: dataKey.Plaintext,
	}

	usEast2Client := new(MockClient)
	usEast2Client.On("EncryptWithContext", mock.Anything, input, mock.Anything).Return(&kms.EncryptOutput{
		CiphertextBlob: encryptedKEK,
		KeyId:          &usEast2ARN,
	}, nil)

	clients := []AWSKMSClient{
		{
			KMS:    preferredClient,
			Region: preferredRegion,
			ARN:    preferredRegionARN,
		},
		{
			KMS:    usEast2Client,
			Region: usEast2,
			ARN:    usEast2ARN,
		},
	}

	KMSKEKs := make(keys, 0)
	for k := range encryptAllRegionsFunc(context.Background(), dataKey, clients) {
		KMSKEKs = append(KMSKEKs, k)
	}

	assert.Equal(t, 2, len(KMSKEKs))
	assert.NotNil(t, KMSKEKs.get(preferredRegion))
	assert.Equal(t, encryptedKEK, KMSKEKs.get(usEast2).EncryptedKEK)
	assert.Equal(t, preferredRegion, KMSKEKs.get(preferredRegion).Region)
	assert.Equal(t, encryptedKey, KMSKEKs.get(preferredRegion).EncryptedKEK)
}

func TestAWSKMS_EncryptAllRegions_ReturnsEmptyKeyOnError(t *testing.T) {
	preferredClient := new(MockClient)
	preferredClient.On("EncryptWithContext", mock.Anything, mock.Anything, mock.Anything).
		Return(nil, errors.New(genericErrorMessage))

	dataKey := &kms.GenerateDataKeyOutput{
		KeyId:     &usEast2ARN,
		Plaintext: plaintextKey,
	}

	clients := []AWSKMSClient{
		{
			Region: preferredRegion,
			KMS:    preferredClient,
			ARN:    preferredRegionARN,
		},
	}

	KMSKEKs := make(keys, 0)
	for k := range encryptAllRegions(context.Background(), dataKey, clients) {
		KMSKEKs = append(KMSKEKs, k)
	}

	assert.Equal(t, 0, len(KMSKEKs))
}

func TestAWSKMS_Get_ReturnsKeyForValidRegion(t *testing.T) {
	var dk envelope

	if assert.NoError(t, json.Unmarshal([]byte(keyJSON), &dk)) {
		assert.Len(t, dk.KMSKEKs, 2)
		assert.Equal(t, dk.KMSKEKs.get(preferredRegion).Region, preferredRegion)
	}
}

func TestAWSKMS_Get_ReturnsNilForInvalidRegion(t *testing.T) {
	var dk envelope

	if assert.NoError(t, json.Unmarshal([]byte(keyJSON), &dk)) {
		assert.Nil(t, dk.KMSKEKs.get(invalidRegion))
	}
}

func TestAWSKMS_GenerateDataKey(t *testing.T) {
	preferredClient := new(MockClient)
	preferredClient.On("GenerateDataKeyWithContext", mock.Anything, mock.Anything, mock.Anything).Return(nil, errors.New(genericErrorMessage))

	usEast2Client := new(MockClient)
	dataKeyOut := &kms.GenerateDataKeyOutput{
		Plaintext: plaintextKey,
	}
	dataKeyIn := &kms.GenerateDataKeyInput{
		KeyId:   &usEast2ARN,
		KeySpec: &keySpec,
	}
	usEast2Client.On("GenerateDataKeyWithContext", mock.Anything, dataKeyIn, mock.Anything).Return(dataKeyOut, nil)

	clients := []AWSKMSClient{
		{
			KMS:    preferredClient,
			Region: preferredRegion,
			ARN:    preferredRegionARN,
		},
		{
			KMS:    usEast2Client,
			Region: usEast2,
			ARN:    usEast2ARN,
		},
	}

	key, err := generateDataKey(context.Background(), clients)

	if assert.NoError(t, err) {
		assert.NotNil(t, key)
		assert.Equal(t, plaintextKey, key.Plaintext)
		preferredClient.AssertCalled(t, "GenerateDataKeyWithContext", mock.Anything, mock.Anything, mock.Anything)
	}
}

func TestAWSKMS_GenerateDataKey_ReturnsErrorWhenAllRegionsFail(t *testing.T) {
	preferredClient := new(MockClient)

	preferredClient.On("GenerateDataKeyWithContext", mock.Anything, mock.Anything, mock.Anything).Return(nil, errors.New(genericErrorMessage))

	clients := []AWSKMSClient{
		{
			KMS:    preferredClient,
			Region: preferredRegion,
			ARN:    preferredRegionARN,
		},
	}

	_, err := generateDataKey(context.Background(), clients)

	assert.Error(t, err)
	preferredClient.AssertCalled(t, "GenerateDataKeyWithContext", mock.Anything, mock.Anything, mock.Anything)
}
