package appencryption

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

const keySize = 32

var (
	genericErrorMessage = "some error message"
	someID              = "something"
	someTimestamp       = time.Now().Round(time.Minute).Unix()
	someBytes           = []byte("someTotallyRandomBytes")
	decryptedBytes      = []byte("someDecryptedData")
	encryptedBytes      = []byte("someEncryptedData")
)

type EnvelopeSuite struct {
	suite.Suite
	crypto        AEAD
	ikCache       cache
	skCache       cache
	partition     partition
	e             envelopeEncryption
	metastore     Metastore
	kms           KeyManagementService
	secretFactory securememory.SecretFactory
	randomSecret  securememory.Secret
	newSecret     securememory.Secret
}

func (suite *EnvelopeSuite) SetupTest() {
	suite.partition = defaultPartition{
		service: "service",
		product: "product",
	}
	suite.metastore = new(MockMetastore)
	suite.kms = new(MockKMS)
	suite.crypto = new(MockCrypto)
	suite.skCache = new(MockCache)
	suite.ikCache = new(MockCache)
	suite.secretFactory = new(MockSecretFactory)

	suite.e = envelopeEncryption{
		partition:        suite.partition,
		Metastore:        suite.metastore,
		KMS:              suite.kms,
		Policy:           NewCryptoPolicy(),
		Crypto:           suite.crypto,
		SecretFactory:    suite.secretFactory,
		systemKeys:       suite.skCache,
		intermediateKeys: suite.ikCache,
	}

	var err error

	suite.randomSecret, err = secretFactory.CreateRandom(keySize)
	assert.NoError(suite.T(), err)

	suite.newSecret, err = secretFactory.New(someBytes)
	assert.NoError(suite.T(), err)
}

func TestEnvelopeSuite(t *testing.T) {
	suite.Run(t, new(EnvelopeSuite))
}

type MockCrypto struct {
	mock.Mock
}

func (c *MockCrypto) Encrypt(data, key []byte) ([]byte, error) {
	// We need to copy the data and key as bytes are set to no access in WithBytesFunc call
	dataCopy := make([]byte, len(data))
	copy(dataCopy, data)

	keyCopy := make([]byte, len(key))
	copy(keyCopy, key)

	ret := c.Called(dataCopy, keyCopy)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (c *MockCrypto) Decrypt(data []byte, key []byte) ([]byte, error) {
	// We need to copy the key as bytes are set to no access in WithBytesFunc call
	keyCopy := make([]byte, len(key))
	copy(keyCopy, key)

	ret := c.Called(data, keyCopy)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

type MockKMS struct {
	mock.Mock
}

func (k *MockKMS) EncryptKey(ctx context.Context, key []byte) ([]byte, error) {
	// We need to copy the key as bytes are set to no access in WithBytesFunc call
	keyCopy := make([]byte, len(key))
	copy(keyCopy, key)

	ret := k.Called(ctx, keyCopy)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (k *MockKMS) DecryptKey(ctx context.Context, key []byte) ([]byte, error) {
	ret := k.Called(ctx, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

type MockMetastore struct {
	mock.Mock
}

func (m *MockMetastore) Load(ctx context.Context, id string, created int64) (*EnvelopeKeyRecord, error) {
	ret := m.Called(ctx, id, created)

	var ekr *EnvelopeKeyRecord
	if b := ret.Get(0); b != nil {
		ekr = b.(*EnvelopeKeyRecord)
	}

	return ekr, ret.Error(1)
}

func (m *MockMetastore) LoadLatest(ctx context.Context, id string) (*EnvelopeKeyRecord, error) {
	ret := m.Called(ctx, id)

	var ekr *EnvelopeKeyRecord
	if b := ret.Get(0); b != nil {
		ekr = b.(*EnvelopeKeyRecord)
	}

	return ekr, ret.Error(1)
}

func (m *MockMetastore) Store(ctx context.Context, id string, created int64, envelope *EnvelopeKeyRecord) (bool, error) {
	ret := m.Called(ctx, id, created, envelope)

	var result bool
	if b := ret.Get(0); b != nil {
		result = b.(bool)
	}

	return result, ret.Error(1)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadSystemKey() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}
	ekr := &EnvelopeKeyRecord{
		Revoked:      false,
		Created:      someTimestamp,
		EncryptedKey: someBytes,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(ekr, nil)
	suite.kms.(*MockKMS).On("DecryptKey", context.Background(), ekr.EncryptedKey).Return(someBytes, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", mock.Anything).Return(suite.newSecret, nil)

	sk, err := suite.e.loadSystemKey(context.Background(), meta)

	if assert.NoError(suite.T(), err) && assert.NotNil(suite.T(), sk) {
		mock.AssertExpectationsForObjects(suite.T(), suite.kms, suite.metastore, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadSystemKey_ReturnsNilIfMetastoreLoadFails() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).
		Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadSystemKey(context.Background(), meta)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadSystemKey_ReturnsNilIfMetastoreLoadReturnsEmptyRecord() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(nil, nil)

	sk, err := suite.e.loadSystemKey(context.Background(), meta)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadSystemKey_ReturnsNilIfKMSDecryptFails() {
	ekr := &EnvelopeKeyRecord{
		EncryptedKey: encryptedBytes,
	}
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(ekr, nil)
	suite.kms.(*MockKMS).On("DecryptKey", context.Background(), encryptedBytes).
		Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadSystemKey(context.Background(), meta)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.kms, suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadIntermediateKey() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}
	parentKeyMeta := KeyMeta{}
	ikEkr := &EnvelopeKeyRecord{
		EncryptedKey:  encryptedBytes,
		ParentKeyMeta: &parentKeyMeta,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(ikEkr, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, skBytes).Return(someBytes, nil)
	suite.skCache.(*MockCache).On("GetOrLoad", parentKeyMeta, mock.Anything).Return(sk, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", mock.Anything).Return(suite.newSecret, nil)

	ik, err := suite.e.loadIntermediateKey(context.Background(), meta)

	if assert.NoError(suite.T(), err) && assert.NotNil(suite.T(), ik) {
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadIntermediateKey_ReturnsNilIfMetastoreLoadFails() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).
		Return(nil, errors.New(genericErrorMessage))

	ik, err := suite.e.loadIntermediateKey(context.Background(), meta)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadIntermediateKey_ReturnsErrorIfMetastoreLoadReturnsNil() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(nil, nil)

	ik, err := suite.e.loadIntermediateKey(context.Background(), meta)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadIntermediateKey_ReturnsErrorIfCryptoFailsToDecryptIKWithSK() {
	meta := KeyMeta{
		ID:      someID,
		Created: someTimestamp,
	}
	parentKeyMeta := KeyMeta{}
	ikEkr := &EnvelopeKeyRecord{
		EncryptedKey:  encryptedBytes,
		ParentKeyMeta: &parentKeyMeta,
	}

	suite.metastore.(*MockMetastore).On("Load", context.Background(), meta.ID, meta.Created).Return(ikEkr, nil)
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, mock.AnythingOfType("[]uint8")).
		Return(nil, errors.New(genericErrorMessage))

	sk, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(suite.T(), err) {
		suite.skCache.(*MockCache).On("GetOrLoad", parentKeyMeta, mock.Anything).Return(sk, nil)

		ik, err := suite.e.loadIntermediateKey(context.Background(), meta)

		assert.Error(suite.T(), err)
		assert.Nil(suite.T(), ik)
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadIntermediateKey_ClosesSKIfCachingNotAllowedByPolicy() {
	meta := KeyMeta{}
	ikEkr := &EnvelopeKeyRecord{
		EncryptedKey:  encryptedBytes,
		ParentKeyMeta: new(KeyMeta),
	}
	suite.e.Policy.CacheSystemKeys = false

	suite.metastore.(*MockMetastore).On("Load", context.Background(), mock.Anything, mock.Anything).Return(ikEkr, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.crypto.(*MockCrypto).On("Decrypt", mock.Anything, mock.Anything).Return(skBytes, nil)
	suite.skCache.(*MockCache).On("GetOrLoad", mock.Anything, mock.Anything).Return(sk, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", mock.Anything).Return(suite.newSecret, nil)

	_, err := suite.e.loadIntermediateKey(context.Background(), meta)

	require.NoError(suite.T(), err)
	assert.True(suite.T(), sk.IsClosed())

	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_ReturnsNilIfLoadLatestFromMetastoreFails() {
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).
		Return(nil, errors.New(genericErrorMessage))

	skBytes, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), skBytes)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_ReturnsNilIfKMSFailsToDecryptSK() {
	skEkr := &EnvelopeKeyRecord{
		ID:           someID,
		Created:      someTimestamp,
		EncryptedKey: encryptedBytes,
	}

	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(skEkr, nil)
	suite.kms.(*MockKMS).On("DecryptKey", context.Background(), skEkr.EncryptedKey).
		Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.kms, suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_CreatesNewKey() {
	skEkr := &EnvelopeKeyRecord{
		ID:           someID,
		Created:      someTimestamp,
		EncryptedKey: encryptedBytes,
	}

	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, nil).Once()
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	suite.kms.(*MockKMS).On("EncryptKey", context.Background(), mock.Anything).
		Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.SystemKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(skEkr, nil)
	suite.kms.(*MockKMS).On("DecryptKey", context.Background(), skEkr.EncryptedKey).
		Return(someBytes, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", someBytes).Return(suite.newSecret, nil)

	sk, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.NoError(suite.T(), err)
	assert.NotNil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.kms, suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_ReturnsErrorWhenMustLoadLatestErrors() {
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, nil).Once()
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	suite.kms.(*MockKMS).On("EncryptKey", context.Background(), mock.Anything).
		Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.SystemKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.EqualError(suite.T(), err, genericErrorMessage)
	assert.Nil(suite.T(), sk)
	mock.AssertExpectationsForObjects(suite.T(), suite.kms, suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_ReturnsErrorIfKMSFailsToEncryptSK() {
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, nil).Once()
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	suite.kms.(*MockKMS).On("EncryptKey", context.Background(), mock.Anything).
		Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.kms, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateSystemKey_MetastoreStoreFailsBecauseOfADuplicateKeyKMSFailsToDecryptShouldReturnError() {
	skEkr := &EnvelopeKeyRecord{
		ID:           someID,
		Created:      someTimestamp,
		EncryptedKey: encryptedBytes,
	}

	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, nil).Once()
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	suite.kms.(*MockKMS).On("EncryptKey", context.Background(), mock.Anything).
		Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.SystemKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(skEkr, nil)
	suite.kms.(*MockKMS).On("DecryptKey", context.Background(), skEkr.EncryptedKey).Return(nil, errors.New(genericErrorMessage))

	sk, err := suite.e.loadLatestOrCreateSystemKey(context.Background(), someID)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), sk)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.kms, suite.metastore, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_IntermediateKeyFromEKRWithSKLookupSuccess() {
	ikEkr := new(EnvelopeKeyRecord)
	ikEkr.ParentKeyMeta = &KeyMeta{Created: someTimestamp + 1}

	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoad", mock.Anything, mock.Anything).Return(sk, nil)
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).Return(ikEkr, nil)
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, skBytes).Return(decryptedBytes, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", mock.Anything).Return(suite.newSecret, nil)

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Nil(suite.T(), err)
	assert.NotNil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_IntermediateKeyFromEKRWithSKLookupFailsShouldReturnError() {
	ikEkr := new(EnvelopeKeyRecord)
	ikEkr.ParentKeyMeta = &KeyMeta{Created: someTimestamp + 1}

	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoad", mock.Anything, mock.Anything).Return(nil, errors.New(genericErrorMessage))
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).Return(ikEkr, nil)

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_ReturnsNilIfCryptoFailsToEncryptIKWithSK() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(nil, errors.New(genericErrorMessage))

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_MetastoreStoreFailsBecauseOfDuplicateKeyLoadLatest() {
	ikEkr := &EnvelopeKeyRecord{
		EncryptedKey:  encryptedBytes,
		ParentKeyMeta: new(KeyMeta),
	}

	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoad", KeyMeta{}, mock.Anything).Return(sk, nil)
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).Return(ikEkr, nil)
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, skBytes).Return(decryptedBytes, nil)
	suite.secretFactory.(*MockSecretFactory).On("New", mock.Anything).Return(suite.newSecret, nil)

	ik, err := suite.e.createIntermediateKey(context.Background())

	if assert.NoError(suite.T(), err) && assert.NotNil(suite.T(), ik) {
		assert.True(suite.T(), suite.randomSecret.IsClosed())
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_MetastoreStoreFailsBecauseOfDuplicateKeyLoadLatestFailureShouldReturnError() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).
		Return(nil, errors.New(genericErrorMessage))

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_MetastoreStoreFailsBecauseOfDuplicateKeyLoadLatestReturnsNilShouldReturnError() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).Return(nil, nil)

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_CreateIntermediateKey_MetastoreStoreFailsBecauseOfDuplicateKeyLoadLatestCryptoFailsToDecryptShouldReturnError() {
	ikEkr := new(EnvelopeKeyRecord)

	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), skBytes).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On("Store", context.Background(), suite.partition.IntermediateKeyID(), mock.Anything, mock.Anything).
		Return(false, errors.New(genericErrorMessage))
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), suite.partition.IntermediateKeyID()).Return(ikEkr, nil)
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, mock.AnythingOfType("[]uint8")).Return(nil, errors.New(genericErrorMessage))

	ik, err := suite.e.createIntermediateKey(context.Background())

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateIntermediateKey_ReturnsErrorIfMetastoreLoadLatestFails() {
	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(nil, errors.New(genericErrorMessage))

	ik, err := suite.e.loadLatestOrCreateIntermediateKey(context.Background(), someID)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ik)
	mock.AssertExpectationsForObjects(suite.T(), suite.metastore)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateIntermediateKey_CryptoFailsToDecryptIKWithSKShouldCreateNewIK() {
	parentKeyMeta := KeyMeta{}
	ikEkr := &EnvelopeKeyRecord{
		Created:       someTimestamp,
		EncryptedKey:  []byte("someotherinvalidbytes"),
		ParentKeyMeta: &parentKeyMeta,
	}

	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(ikEkr, nil)
	sk, skBytes := getKeyAndKeyBytes(suite.T())
	suite.skCache.(*MockCache).On("GetOrLoad", parentKeyMeta, mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, skBytes).Return(nil, errors.New(genericErrorMessage))
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), mock.AnythingOfType("[]uint8")).Return(encryptedBytes, nil)
	suite.metastore.(*MockMetastore).On(
		"Store",
		context.Background(),
		suite.partition.IntermediateKeyID(),
		mock.Anything,
		mock.Anything,
	).Return(
		true,
		nil,
	).Run(func(args mock.Arguments) {
		ekr := args.Get(3).(*EnvelopeKeyRecord)
		assert.Equal(suite.T(), encryptedBytes, ekr.EncryptedKey)
	})

	ik, err := suite.e.loadLatestOrCreateIntermediateKey(context.Background(), someID)

	if assert.NoError(suite.T(), err) && assert.NotNil(suite.T(), ik) {
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_LoadLatestOrCreateIntermediateKey_ReturnsErrorIfSKForIKIsInvalidStateAndCreateIntermediateKeyFails() {
	parentKeyMeta := KeyMeta{}
	ikEkr := &EnvelopeKeyRecord{
		Created:       someTimestamp,
		EncryptedKey:  encryptedBytes,
		ParentKeyMeta: &parentKeyMeta,
	}

	suite.metastore.(*MockMetastore).On("LoadLatest", context.Background(), someID).Return(ikEkr, nil)

	sk, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(suite.T(), err) {
		suite.skCache.(*MockCache).On("GetOrLoad", parentKeyMeta, mock.Anything).Return(sk, nil)
		suite.crypto.(*MockCrypto).On("Decrypt", ikEkr.EncryptedKey, mock.AnythingOfType("[]uint8")).Return(nil, errors.New(genericErrorMessage))
		suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
		suite.skCache.(*MockCache).On("GetOrLoadLatest", suite.partition.SystemKeyID(), mock.Anything).Return(sk, nil)
		suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), mock.AnythingOfType("[]uint8")).Return(nil, errors.New(genericErrorMessage))

		ik, err := suite.e.loadLatestOrCreateIntermediateKey(context.Background(), someID)

		assert.Error(suite.T(), err)
		assert.Nil(suite.T(), ik)
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.metastore, suite.skCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_EncryptPayload() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	ik, ikBytes := getKeyAndKeyBytes(suite.T())
	suite.ikCache.(*MockCache).On("GetOrLoadLatest", suite.partition.IntermediateKeyID(), mock.Anything).Return(ik, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", someBytes, mock.AnythingOfType("[]uint8")).Return(someBytes, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), ikBytes).Return(encryptedBytes, nil)

	drr, err := suite.e.EncryptPayload(context.Background(), someBytes)

	if assert.NoError(suite.T(), err) {
		assert.Equal(suite.T(), encryptedBytes, drr.Key.EncryptedKey)
		assert.True(suite.T(), suite.randomSecret.IsClosed())
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.ikCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_EncryptPayload_ReturnsErrorIfGetOrLoadLatestFromCacheFails() {
	suite.ikCache.(*MockCache).On("GetOrLoadLatest", suite.partition.IntermediateKeyID(), mock.Anything).
		Return(nil, errors.New(genericErrorMessage))

	drr, err := suite.e.EncryptPayload(context.Background(), someBytes)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), drr)
	mock.AssertExpectationsForObjects(suite.T(), suite.ikCache)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_EncryptPayload_ReturnsErrorIfCryptoFailsToEncryptPayload() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)

	ik, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(suite.T(), err) {
		suite.ikCache.(*MockCache).On("GetOrLoadLatest", suite.partition.IntermediateKeyID(), mock.Anything).Return(ik, nil)
		suite.crypto.(*MockCrypto).On("Encrypt", someBytes, mock.AnythingOfType("[]uint8")).Return(nil, errors.New(genericErrorMessage))

		drr, err := suite.e.EncryptPayload(context.Background(), someBytes)

		assert.Error(suite.T(), err)
		assert.Nil(suite.T(), drr)
		assert.True(suite.T(), suite.randomSecret.IsClosed())
		mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.ikCache, suite.secretFactory)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_EncryptPayload_ReturnsErrorIfCryptoFailsToEncryptDrr() {
	suite.secretFactory.(*MockSecretFactory).On("CreateRandom", mock.Anything).Return(suite.randomSecret, nil)
	ik, ikBytes := getKeyAndKeyBytes(suite.T())
	suite.ikCache.(*MockCache).On("GetOrLoadLatest", suite.partition.IntermediateKeyID(), mock.Anything).Return(ik, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", someBytes, mock.AnythingOfType("[]uint8")).Return(someBytes, nil)
	suite.crypto.(*MockCrypto).On("Encrypt", mock.AnythingOfType("[]uint8"), ikBytes).Return(nil, errors.New(genericErrorMessage))

	drr, err := suite.e.EncryptPayload(context.Background(), someBytes)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), drr)
	assert.True(suite.T(), suite.randomSecret.IsClosed())
	mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.ikCache, suite.secretFactory)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord() {
	meta := &KeyMeta{
		ID: suite.partition.IntermediateKeyID(),
	}
	drr := &DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey:  someBytes,
			ParentKeyMeta: meta,
		},
		Data: encryptedBytes,
	}

	suite.assertCanDecrypt(drr)
}

func (suite *EnvelopeSuite) assertCanDecrypt(drr *DataRowRecord) {
	meta := drr.Key.ParentKeyMeta

	ik, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(suite.T(), err) {
		suite.ikCache.(*MockCache).On("GetOrLoad", *meta, mock.Anything).Return(ik, nil)
		suite.crypto.(*MockCrypto).On("Decrypt", someBytes, mock.AnythingOfType("[]uint8")).Return(someBytes, nil)
		suite.crypto.(*MockCrypto).On("Decrypt", encryptedBytes, someBytes).Return(decryptedBytes, nil)

		drrBytes, err := suite.e.DecryptDataRowRecord(context.Background(), *drr)

		if assert.NoError(suite.T(), err) {
			assert.Equal(suite.T(), decryptedBytes, drrBytes)
			mock.AssertExpectationsForObjects(suite.T(), suite.crypto, suite.ikCache)
		}
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_WithSuffixedPartitionAndNonSuffixedKey() {
	// Note that this DRR's parent key ID is not suffixed
	drr := &DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey: someBytes,
			ParentKeyMeta: &KeyMeta{
				ID: suite.partition.IntermediateKeyID(),
			},
		},
		Data: encryptedBytes,
	}

	// Now swap out the envelopeEncryption.partition with a suffixedPartition
	suite.e.partition = suffixedPartition{
		defaultPartition: suite.e.partition.(defaultPartition),
		suffix:           "some-suffix",
	}

	suite.assertCanDecrypt(drr)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_WithoutParentKeyMetaShouldFail() {
	drr := DataRowRecord{
		Key:  new(EnvelopeKeyRecord),
		Data: someBytes,
	}

	drrBytes, err := suite.e.DecryptDataRowRecord(context.Background(), drr)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), drrBytes)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_WithDifferentPartitionShouldFail() {
	meta := &KeyMeta{
		ID: someID, // someID != suite.partition.IntermediateKeyID()
	}
	drr := DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey:  someBytes,
			ParentKeyMeta: meta,
		},
		Data: encryptedBytes,
	}

	drrBytes, err := suite.e.DecryptDataRowRecord(context.Background(), drr)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), drrBytes)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_ReturnsErrorWhenDataRowRecordKeyIsMissing() {
	drrBytes, err := suite.e.DecryptDataRowRecord(context.Background(), DataRowRecord{})

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), drrBytes)
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_ClosesKeyIfCachingNotAllowedByPolicy() {
	meta := &KeyMeta{
		ID: suite.partition.IntermediateKeyID(),
	}
	drr := &DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey:  someBytes,
			ParentKeyMeta: meta,
		},
		Data: encryptedBytes,
	}

	suite.e.Policy.CacheIntermediateKeys = false

	ik, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(suite.T(), err) {
		suite.ikCache.(*MockCache).On("GetOrLoad", *meta, mock.Anything).Return(ik, nil)
		suite.crypto.(*MockCrypto).On("Decrypt", someBytes, mock.AnythingOfType("[]uint8")).Return(someBytes, nil)
		suite.crypto.(*MockCrypto).On("Decrypt", encryptedBytes, someBytes).Return(decryptedBytes, nil)

		_, err := suite.e.DecryptDataRowRecord(context.Background(), *drr)
		require.NoError(suite.T(), err)

		assert.True(suite.T(), ik.IsClosed())
		mock.AssertExpectationsForObjects(suite.T(), suite.ikCache)
	}
}

func (suite *EnvelopeSuite) TestEnvelopeEncryption_DecryptDataRowRecord_ReturnsErrorIfGetOrLoadFromCacheFails() {
	meta := &KeyMeta{
		ID: suite.partition.IntermediateKeyID(),
	}
	drr := DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey:  someBytes,
			ParentKeyMeta: meta,
		},
		Data: encryptedBytes,
	}

	suite.ikCache.(*MockCache).On("GetOrLoad", *meta, mock.Anything).Return(nil, errors.New(genericErrorMessage))

	ikBytes, err := suite.e.DecryptDataRowRecord(context.Background(), drr)

	assert.Error(suite.T(), err)
	assert.Nil(suite.T(), ikBytes)
	mock.AssertExpectationsForObjects(suite.T(), suite.ikCache)
}

func (suite *EnvelopeSuite) Test_KeyReloader_Load() {
	called := false

	reloader := &reloader{
		loader: keyLoaderFunc(func() (*internal.CryptoKey, error) {
			called = true
			return nil, nil
		}),
	}

	k, err := reloader.Load()
	assert.Nil(suite.T(), k)
	assert.NoError(suite.T(), err)
	assert.True(suite.T(), called)
}

func (suite *EnvelopeSuite) Test_KeyReloader_IsInvalid() {
	k, _ := getKeyAndKeyBytes(suite.T())
	called := false

	reloader := &reloader{
		isInvalidFunc: func(key *internal.CryptoKey) bool {
			called = true

			assert.Equal(suite.T(), k, key)
			return false
		},
	}

	reloader.IsInvalid(k)

	assert.True(suite.T(), called)
}

func (suite *EnvelopeSuite) Test_KeyReloader_Close() {
	reloader := &reloader{
		loader: keyLoaderFunc(func() (*internal.CryptoKey, error) {
			k, _ := getKeyAndKeyBytes(suite.T())
			return k, nil
		}),
	}
	loadTestKey := func() *internal.CryptoKey {
		k, _ := reloader.Load()
		return k
	}

	var keys []*internal.CryptoKey
	keys = append(keys, loadTestKey(), loadTestKey())

	for _, k := range keys {
		assert.False(suite.T(), k.IsClosed())
	}

	reloader.Close()

	for _, k := range keys {
		assert.True(suite.T(), k.IsClosed())
	}
}

func TestKeyMeta_String(t *testing.T) {
	meta := KeyMeta{
		Created: someTimestamp,
		ID:      someID,
	}

	assert.Contains(t, meta.String(), "KeyMeta [keyId=")
}

func Test_DecryptRow_ReturnsNilIfCryptoFailsToDecrypt(t *testing.T) {
	drr := DataRowRecord{
		Key: &EnvelopeKeyRecord{
			EncryptedKey: encryptedBytes,
		},
	}

	crypto := new(MockCrypto)
	crypto.On("Decrypt", drr.Key.EncryptedKey, mock.Anything).Return(nil, errors.New(genericErrorMessage))

	ik, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	if assert.NoError(t, err) {
		decryptedBytes, err := decryptRow(ik, drr, crypto)

		assert.Error(t, err)
		assert.Nil(t, decryptedBytes)
	}
}

func TestEnvelopeEncryption_Close(t *testing.T) {
	data := []byte("somedata")

	sec, err := secretFactory.New(data)
	if assert.NoError(t, err) {
		m := new(MockSecretFactory)
		m.On("New", data).Return(sec, nil)

		cache := newKeyCache(NewCryptoPolicy())
		key, _ := internal.NewCryptoKey(m, 123456, false, data)

		cache.keys["testing"] = cacheEntry{
			key: key,
		}

		e := &envelopeEncryption{
			intermediateKeys: cache,
		}

		assert.False(t, sec.IsClosed())

		e.Close()

		assert.True(t, sec.IsClosed())
	}
}

func getKeyAndKeyBytes(t *testing.T) (*internal.CryptoKey, []byte) {
	key, err := internal.GenerateKey(secretFactory, someTimestamp, keySize)
	assert.NoError(t, err)

	keyBytes, err := internal.WithKeyFunc(key, func(bytes []byte) ([]byte, error) {
		bytesCopy := make([]byte, len(bytes))
		copy(bytesCopy, bytes)
		return bytesCopy, nil
	})
	assert.NoError(t, err)

	return key, keyBytes
}
