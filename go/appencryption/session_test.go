package appencryption

import (
	"context"
	"fmt"
	"testing"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

type MockSecretFactory struct {
	mock.Mock
}

func (s *MockSecretFactory) New(b []byte) (securememory.Secret, error) {
	ret := s.Called(b)

	var newSecret securememory.Secret
	if b := ret.Get(0); b != nil {
		newSecret = b.(securememory.Secret)
	}

	return newSecret, ret.Error(1)
}

func (s *MockSecretFactory) CreateRandom(size int) (securememory.Secret, error) {
	ret := s.Called(size)

	var newSecret securememory.Secret
	if b := ret.Get(0); b != nil {
		newSecret = b.(securememory.Secret)
	}

	return newSecret, ret.Error(1)
}

type MockEncryption struct {
	mock.Mock
}

func (c *MockEncryption) EncryptPayload(ctx context.Context, data []byte) (*DataRowRecord, error) {
	var (
		ret = c.Called(ctx, data)
		drr *DataRowRecord
	)

	if b := ret.Get(0); b != nil {
		drr = b.(*DataRowRecord)
	}

	return drr, ret.Error(1)
}

func (c *MockEncryption) DecryptDataRowRecord(ctx context.Context, d DataRowRecord) ([]byte, error) {
	var (
		ret   = c.Called(ctx, d)
		bytes []byte
	)

	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (c *MockEncryption) Close() error {
	ret := c.Called()

	return ret.Error(0)
}

type MockCache struct {
	mock.Mock
}

func (c *MockCache) GetOrLoad(id KeyMeta, loader keyLoader) (*internal.CryptoKey, error) {
	var (
		ret = c.Called(id, loader)
		key *internal.CryptoKey
	)

	if b := ret.Get(0); b != nil {
		key = b.(*internal.CryptoKey)
	}

	return key, ret.Error(1)
}

func (c *MockCache) GetOrLoadLatest(id string, loader keyLoader) (*internal.CryptoKey, error) {
	var (
		ret = c.Called(id, loader)
		key *internal.CryptoKey
	)

	if b := ret.Get(0); b != nil {
		key = b.(*internal.CryptoKey)
	}

	return key, ret.Error(1)
}

func (c *MockCache) Close() error {
	ret := c.Called()

	return ret.Error(0)
}

func TestNewSessionFactory(t *testing.T) {
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	require.NotNil(t, factory)
	assert.IsType(t, new(keyCache), factory.systemKeys)
	assert.IsType(t, new(memguard.SecretFactory), factory.SecretFactory)
	assert.Nil(t, factory.sessionCache)
}

func TestNewSessionFactory_WithSessionCache(t *testing.T) {
	policy := &CryptoPolicy{
		CacheSessions: true,
	}
	factory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)

	defer factory.Close()

	require.NotNil(t, factory)
	assert.NotNil(t, factory.sessionCache)

	sess, err := factory.GetSession("testing")
	require.NoError(t, err)
	assert.IsType(t, new(sharedEncryption), sess.encryption)
	sess.Close()
}

func TestNewSessionFactory_NoSKCache(t *testing.T) {
	policy := &CryptoPolicy{
		CacheSystemKeys: false,
	}
	factory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)
	assert.NotNil(t, factory)
	assert.IsType(t, new(neverCache), factory.systemKeys)
	assert.IsType(t, new(memguard.SecretFactory), factory.SecretFactory)
}

func TestNewSessionFactory_WithOptions(t *testing.T) {
	factory := NewSessionFactory(new(Config), nil, nil, nil, WithSecretFactory(new(MockSecretFactory)), WithMetrics(false))
	assert.NotNil(t, factory)
	assert.IsType(t, new(keyCache), factory.systemKeys)
	assert.IsType(t, new(MockSecretFactory), factory.SecretFactory)
}

func TestSessionFactory_GetSession(t *testing.T) {
	policy := NewCryptoPolicy()
	policy.CacheIntermediateKeys = false

	sessionFactory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)

	sess, err := sessionFactory.GetSession("testing")
	if assert.NoError(t, err) {
		assert.NotNil(t, sess.encryption)
		ik := sess.encryption.(*envelopeEncryption).intermediateKeys
		assert.IsType(t, new(neverCache), ik)
	}
}

func TestSessionFactory_GetSession_CanCacheIntermediateKeys(t *testing.T) {
	policy := NewCryptoPolicy()
	sessionFactory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)
	policy.CacheIntermediateKeys = true

	sess, err := sessionFactory.GetSession("testing")
	if assert.NoError(t, err) {
		assert.NotNil(t, sess.encryption)
		ik := sess.encryption.(*envelopeEncryption).intermediateKeys
		assert.IsType(t, new(keyCache), ik)
	}
}

func TestSessionFactory_GetSession_EmptyPartitionIdFails(t *testing.T) {
	policy := NewCryptoPolicy()
	sessionFactory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)

	sess, err := sessionFactory.GetSession("")
	assert.Error(t, err)
	assert.Nil(t, sess)
}

func TestSessionFactory_Close(t *testing.T) {
	factory := NewSessionFactory(new(Config), nil, nil, nil)

	mockCache := new(MockCache)
	mockCache.On("Close").Return(nil)
	factory.systemKeys = mockCache

	assert.NoError(t, factory.Close())
	mockCache.AssertCalled(t, "Close")
}

func TestSession_Close(t *testing.T) {
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	session, _ := factory.GetSession("testing")

	mockEnvelopeEncryption := new(MockEncryption)
	mockEnvelopeEncryption.On("Close").Return(nil)
	session.encryption = mockEnvelopeEncryption

	assert.NoError(t, session.Close())
	mockEnvelopeEncryption.AssertCalled(t, "Close")
}

func TestSession_Encrypt(t *testing.T) {
	someBytes := []byte("somePayload")
	encryptedBytes := []byte("hdfjskahfkjdsahkjfdhsaklfhdsakl")
	dataRowRecord := &DataRowRecord{
		Data: encryptedBytes,
	}
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	session, _ := factory.GetSession("testing")

	mockEnvelopeEncryption := new(MockEncryption)
	session.encryption = mockEnvelopeEncryption
	mockEnvelopeEncryption.On("EncryptPayload", context.Background(), someBytes).Return(dataRowRecord, nil)

	record, e := session.Encrypt(someBytes)

	assert.NoError(t, e)
	assert.Equal(t, encryptedBytes, record.Data)
}

func TestSession_EncryptContext(t *testing.T) {
	someBytes := []byte("somePayload")
	encryptedBytes := []byte("hdfjskahfkjdsahkjfdhsaklfhdsakl")
	dataRowRecord := &DataRowRecord{
		Data: encryptedBytes,
	}
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	session, _ := factory.GetSession("testing")

	mockEnvelopeEncryption := new(MockEncryption)
	session.encryption = mockEnvelopeEncryption
	// Using an un-exported struct to avoid accidental collisions and linting errors
	ctx := context.WithValue(context.Background(), new(neverCache), "someValue")
	mockEnvelopeEncryption.On("EncryptPayload", ctx, someBytes).Return(dataRowRecord, nil)

	record, e := session.EncryptContext(ctx, someBytes)

	assert.NoError(t, e)
	assert.Equal(t, encryptedBytes, record.Data)
}

func TestSession_Decrypt(t *testing.T) {
	someBytes := []byte("somePayload")
	dataRowRecord := DataRowRecord{
		Data: []byte("hdfjskahfkjdsahkjfdhsaklfhdsakl"),
	}
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	session, _ := factory.GetSession("testing")

	mockEnvelopeEncryption := new(MockEncryption)
	session.encryption = mockEnvelopeEncryption
	mockEnvelopeEncryption.On("DecryptDataRowRecord", context.Background(), dataRowRecord).Return(someBytes, nil)

	result, e := session.Decrypt(dataRowRecord)
	assert.NoError(t, e)
	assert.Equal(t, someBytes, result)
}

func TestSession_DecryptContext(t *testing.T) {
	someBytes := []byte("somePayload")
	dataRowRecord := DataRowRecord{
		Data: []byte("hdfjskahfkjdsahkjfdhsaklfhdsakl"),
	}
	factory := NewSessionFactory(new(Config), nil, nil, nil)
	session, _ := factory.GetSession("testing")

	mockEnvelopeEncryption := new(MockEncryption)
	session.encryption = mockEnvelopeEncryption
	// Using an un-exported struct to avoid accidental collisions and linting errors
	ctx := context.WithValue(context.Background(), new(neverCache), "someValue")
	mockEnvelopeEncryption.On("DecryptDataRowRecord", ctx, dataRowRecord).Return(someBytes, nil)

	result, e := session.DecryptContext(ctx, dataRowRecord)
	assert.NoError(t, e)
	assert.Equal(t, someBytes, result)
}

type MockPersistenceStore struct {
	mock.Mock
}

func (s *MockPersistenceStore) Store(ctx context.Context, key string, d DataRowRecord) error {
	ret := s.Called(ctx, key, d)
	return ret.Error(0)
}

func (s *MockPersistenceStore) Load(ctx context.Context, key string) (*DataRowRecord, error) {
	ret := s.Called(ctx, key)
	return ret.Get(0).(*DataRowRecord), ret.Error(1)
}

func TestSession_Store(t *testing.T) {
	type test struct {
		encryptError     error
		persistenceError error
		withContext      bool
	}

	origTests := map[string]test{
		"success":             {encryptError: nil, persistenceError: nil},
		"encryption failure":  {encryptError: fmt.Errorf("some encryption error"), persistenceError: nil},
		"persistence failure": {encryptError: nil, persistenceError: fmt.Errorf("some storage error")},
	}

	tests := make(map[string]test)

	for name, tc := range origTests {
		tests[name] = tc
		tests[fmt.Sprintf("%s/withContext", name)] = test{
			encryptError:     tc.encryptError,
			persistenceError: tc.persistenceError,
			withContext:      true,
		}
	}

	for name := range tests {
		tc := tests[name]

		t.Run(name, func(t *testing.T) {
			ctx := context.Background()
			var ctxMatcher interface{} = mock.MatchedBy(func(ctx context.Context) bool { return true })
			if tc.withContext {
				ctxMatcher = ctx
			}

			payload := []byte("some secret data")
			encryptedPayload := new(DataRowRecord)

			mockEnvelopeEncryption := new(MockEncryption)
			mockEnvelopeEncryption.On("EncryptPayload", ctxMatcher, payload).Return(encryptedPayload, tc.encryptError)

			persistenceKey := "some-unique-id"
			session := &Session{encryption: mockEnvelopeEncryption}

			mockPersistenceStore := new(MockPersistenceStore)

			if tc.encryptError == nil {
				mockPersistenceStore.On("Store", ctxMatcher, persistenceKey, *encryptedPayload).Return(tc.persistenceError)
			}

			var err error

			if tc.withContext {
				err = session.StoreContext(ctx, persistenceKey, payload, mockPersistenceStore)
			} else {
				err = session.Store(persistenceKey, payload, mockPersistenceStore)
			}

			switch {
			case tc.encryptError != nil:
				assert.Equal(t, tc.encryptError, err)
			case tc.persistenceError != nil:
				assert.Equal(t, tc.persistenceError, err)
			default:
				require.NoError(t, err)
			}

			mockEnvelopeEncryption.AssertExpectations(t)
			mockPersistenceStore.AssertExpectations(t)
		})
	}
}

func TestSession_Load(t *testing.T) {
	type test struct {
		expected         []byte
		decryptError     error
		persistenceError error
		withContext      bool
	}

	origTests := map[string]test{
		"success":             {expected: []byte("some secret"), decryptError: nil, persistenceError: nil},
		"persistence failure": {decryptError: nil, persistenceError: fmt.Errorf("some storage error")},
		"decryption failure":  {decryptError: fmt.Errorf("some decryption error"), persistenceError: nil},
	}

	tests := make(map[string]test)

	for name, tc := range origTests {
		tests[name] = tc
		tests[fmt.Sprintf("%s/withContext", name)] = test{
			expected:         tc.expected,
			decryptError:     tc.decryptError,
			persistenceError: tc.persistenceError,
			withContext:      true,
		}
	}

	for name := range tests {
		tc := tests[name]

		t.Run(name, func(t *testing.T) {
			persistenceKey := "some-unique-id"
			encryptedPayload := new(DataRowRecord)
			mockPersistenceStore := new(MockPersistenceStore)
			mockEnvelopeEncryption := new(MockEncryption)
			session := &Session{encryption: mockEnvelopeEncryption}

			var ctxMatcher interface{}
			var loadFunc func() ([]byte, error)

			if tc.withContext {
				ctx := context.Background()
				ctxMatcher = ctx
				loadFunc = func() ([]byte, error) { return session.LoadContext(ctx, persistenceKey, mockPersistenceStore) }
			} else {
				ctxMatcher = mock.MatchedBy(func(ctx context.Context) bool { return true })
				loadFunc = func() ([]byte, error) { return session.Load(persistenceKey, mockPersistenceStore) }
			}

			mockPersistenceStore.On("Load", ctxMatcher, persistenceKey).Return(encryptedPayload, tc.persistenceError)
			if tc.persistenceError == nil {
				mockEnvelopeEncryption.On("DecryptDataRowRecord", ctxMatcher, *encryptedPayload).Return(tc.expected, tc.decryptError)
			}

			data, err := loadFunc()
			assert.Equal(t, tc.expected, data)

			switch {
			case tc.decryptError != nil:
				assert.Equal(t, tc.decryptError, err)
			case tc.persistenceError != nil:
				assert.Equal(t, tc.persistenceError, err)
			default:
				require.NoError(t, err)
			}

			mockPersistenceStore.AssertExpectations(t)
			mockEnvelopeEncryption.AssertExpectations(t)
		})
	}
}

type MockDynamoDBMetastore struct {
	*MockMetastore
}

func (m *MockDynamoDBMetastore) GetRegionSuffix() string {
	args := m.Called()
	return args.String(0)
}

func TestSessionFactory_GetSession_DefaultPartition(t *testing.T) {
	factory := NewSessionFactory(new(Config), nil, nil, nil)

	sess, err := factory.GetSession("abc")
	assert.NoError(t, err)

	e := sess.encryption.(*envelopeEncryption)
	_, ok := e.partition.(defaultPartition)
	assert.True(t, ok, "expected type defaultParition")
}

func TestSessionFactory_GetSession_SuffixedPartition(t *testing.T) {
	store := &MockDynamoDBMetastore{MockMetastore: new(MockMetastore)}
	store.On("GetRegionSuffix").Return("suffix")

	factory := NewSessionFactory(new(Config), store, nil, nil)

	sess, err := factory.GetSession("abc")
	assert.NoError(t, err)

	e := sess.encryption.(*envelopeEncryption)
	_, ok := e.partition.(suffixedPartition)
	assert.True(t, ok, "expected type suffixedPartition")
}

func TestSessionFactory_GetSession_Blank_GetSuffix_DefaultPartition(t *testing.T) {
	store := &MockDynamoDBMetastore{MockMetastore: new(MockMetastore)}
	store.On("GetRegionSuffix").Return("")

	factory := NewSessionFactory(new(Config), store, nil, nil)

	sess, err := factory.GetSession("abc")
	assert.NoError(t, err)

	e := sess.encryption.(*envelopeEncryption)
	_, ok := e.partition.(defaultPartition)
	assert.True(t, ok, "expected type defaultPartition")
}

type mockSessionCache struct {
	mock.Mock
}

func (m *mockSessionCache) Get(id string) (*Session, error) {
	ret := m.Called(id)
	if s := ret.Get(0); s != nil {
		return s.(*Session), ret.Error(1)
	}

	return nil, ret.Error(1)
}

func (m *mockSessionCache) Count() int {
	ret := m.Called()

	return ret.Int(0)
}

func (m *mockSessionCache) Close() {
	m.Called()
}

func TestSessionFactory_GetSession_NoSessionCache(t *testing.T) {
	policy := NewCryptoPolicy()
	policy.CacheSessions = false

	sessionFactory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)

	cache := new(mockSessionCache)
	sessionFactory.sessionCache = cache

	sess, err := sessionFactory.GetSession("testing")
	require.NoError(t, err)

	assert.NotNil(t, sess)
	cache.AssertNotCalled(t, "Get", "testing")
}

func TestSessionFactory_GetSession_SessionCache(t *testing.T) {
	policy := NewCryptoPolicy()
	policy.CacheSessions = true

	sessionFactory := NewSessionFactory(&Config{
		Policy: policy,
	}, nil, nil, nil)

	id := "testing"

	cache := new(mockSessionCache)
	cache.On("Get", id).Return(new(Session), nil)

	sessionFactory.sessionCache = cache

	sess, err := sessionFactory.GetSession(id)
	require.NoError(t, err)

	assert.NotNil(t, sess)
	cache.AssertCalled(t, "Get", "testing")
}
