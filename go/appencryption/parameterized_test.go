package appencryption

import (
	"context"
	"math/rand"
	"sort"
	"strconv"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

const (
	RETIRED = "RETIRED"
	VALID   = "VALID"
	EMPTY   = "EMPTY"
	PRODUCT = "PRODUCT"

	letterBytes = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
)

var (
	keyStates = [...]string{RETIRED, VALID, EMPTY}
)

func TestParameters(t *testing.T) {
	for i := 0; i < len(keyStates); i++ {
		for j := 0; j < len(keyStates); j++ {
			for k := 0; k < len(keyStates); k++ {
				for l := 0; l < len(keyStates); l++ {
					runParameterizedTest(t, keyStates[i], keyStates[j], keyStates[k], keyStates[l])
				}
			}
		}
	}
}

func runParameterizedTest(t *testing.T, cacheIK, metaIK, cacheSK, metaSK string) {
	testID, partitionID, serviceID := generateTestID(cacheIK, metaIK, cacheSK, metaSK)
	t.Run(testID, func(t *testing.T) {
		payload := generatePayload()
		crypto := createCryptoMock(payload)

		sk, _ := internal.GenerateKey(secretFactory, time.Now().Add(-10*time.Hour).Unix(), AES256KeySize)
		defer sk.Close()

		ik, _ := internal.GenerateKey(secretFactory, time.Now().Add(-10*time.Hour).Unix(), AES256KeySize)
		defer ik.Close()

		km := new(MockKMS)
		km.On("EncryptKey", mock.Anything, mock.Anything).Return([]byte("encryptedSK"), nil)
		km.On("DecryptKey", mock.Anything, []byte("encryptedSK")).Return([]byte("decryptedSK"), nil)

		partition := newPartition(partitionID, serviceID, PRODUCT)

		metastore := createMetastoreSpy(context.Background(), metaIK, metaSK, ik, sk, crypto, partition, km)

		policy := NewCryptoPolicy()

		ikCacheEncrypt, skCacheEncrypt := createCache(partition, cacheIK, cacheSK, ik, sk, policy)
		defer skCacheEncrypt.Close()

		sessionEncrypt := createSession(crypto, metastore, km, secretFactory, policy, partition, ikCacheEncrypt, skCacheEncrypt)
		defer sessionEncrypt.Close() // closing the session closes the ikCache

		record, err := sessionEncrypt.Encrypt(context.Background(), payload)

		if assert.NoError(t, err) && assert.NotNil(t, record) {
			ekStates := &encryptKeyStates{
				cacheIK: cacheIK,
				metaIK:  metaIK,
				cacheSK: cacheSK,
				metaSK:  metaSK,
			}

			verifyEncryptFlow(t, &metastore.Mock, ekStates, partition.IntermediateKeyID(), partition.SystemKeyID())

			ikCacheDecrypt, skCacheDecrypt := createCache(partition, cacheIK, cacheSK, ik, sk, policy)
			defer skCacheDecrypt.Close()

			sessionDecrypt := createSession(crypto, metastore, km, secretFactory, policy, partition, ikCacheDecrypt, skCacheDecrypt)
			defer sessionDecrypt.Close() // closing the session closes the ikCache

			bytes, err := sessionDecrypt.Decrypt(context.Background(), *record)

			if assert.NoError(t, err) && assert.NotNil(t, bytes) {
				assert.Equal(t, payload, bytes)
				dkStates := &decryptKeyStates{
					cacheIK: cacheIK,
					cacheSK: cacheSK}

				verifyDecryptFlow(t, &metastore.Mock, dkStates, partition.IntermediateKeyID(), partition.SystemKeyID())
			}
		}
	})
}

type spyMetastore struct {
	mock.Mock
	envelopes map[string]map[int64]*EnvelopeKeyRecord
}

func (s *spyMetastore) Load(ctx context.Context, id string, created int64) (*EnvelopeKeyRecord, error) {
	ret := s.Called(ctx, id, created)

	// If the Load call is not mocked, retrieve the relevant key from the backing map
	if ret == nil {
		if ret, ok := s.envelopes[id][created]; ok {
			return ret, nil
		}

		return nil, nil
	}

	return ret.Get(0).(*EnvelopeKeyRecord), ret.Error(1)
}

func (s *spyMetastore) LoadLatest(ctx context.Context, id string) (*EnvelopeKeyRecord, error) {
	ret := s.Called(ctx, id)

	// If the LoadLatest call is not mocked, retrieve the latest key from the backing map
	if ret == nil {
		if keyIDMap, ok := s.envelopes[id]; ok {
			// Sort submap by key since it is the created time
			var createdKeys []int64
			for created := range keyIDMap {
				createdKeys = append(createdKeys, created)
			}

			sort.Slice(createdKeys, func(i, j int) bool { return createdKeys[i] < createdKeys[j] })

			latestCreated := createdKeys[len(createdKeys)-1]

			if ret, ok := keyIDMap[latestCreated]; ok {
				return ret, nil
			}
		}

		return nil, nil
	}

	return ret.Get(0).(*EnvelopeKeyRecord), ret.Error(1)
}

func (s *spyMetastore) Store(ctx context.Context, id string, created int64, envelope *EnvelopeKeyRecord) (bool, error) {
	ret := s.Called(ctx, id, created, envelope)

	// If the Store call is not mocked, store the key to the backing map
	if ret == nil {
		// If a value already exists, do not store to the map
		if _, ok := s.envelopes[id][created]; ok {
			return false, nil
		}

		// If first time, need to initialize nested map
		if _, ok := s.envelopes[id]; !ok {
			s.envelopes[id] = make(map[int64]*EnvelopeKeyRecord)
		}

		// We populate a map so that we can actually decrypt the record in case a new key gets created
		s.envelopes[id][created] = envelope

		return true, nil
	}

	return ret.Get(0).(bool), ret.Error(1)
}

func createRevokedKey(src *internal.CryptoKey, factory securememory.SecretFactory) *internal.CryptoKey {
	bytes, _ := internal.WithKeyFunc(src, func(bytes []byte) ([]byte, error) {
		bytesCopy := make([]byte, len(bytes))
		copy(bytesCopy, bytes)
		return bytesCopy, nil
	})

	key, err := internal.NewCryptoKey(factory, src.Created(), true, bytes)
	if err != nil {
		panic(err)
	}

	return key
}

func createSession(crypto AEAD, metastore Metastore, kms KeyManagementService, factory securememory.SecretFactory,
	policy *CryptoPolicy, partition partition, ikCache cache, skCache cache) *Session {
	return &Session{
		encryption: &envelopeEncryption{
			partition:        partition,
			Metastore:        metastore,
			KMS:              kms,
			Policy:           policy,
			Crypto:           crypto,
			SecretFactory:    factory,
			systemKeys:       skCache,
			intermediateKeys: ikCache,
		}}
}

func createCache(partition partition, cacheIK, cacheSK string, intermediateKey, systemKey *internal.CryptoKey,
	policy *CryptoPolicy) (cache, cache) {
	var ikCache, skCache cache
	skCache = newKeyCache(policy)
	ikCache = newKeyCache(policy)

	sk := systemKey
	ik := intermediateKey

	if cacheSK != EMPTY {
		if cacheSK == RETIRED {
			// We create a revoked copy of the same key
			sk = createRevokedKey(sk, secretFactory)
		}

		meta := &KeyMeta{
			ID:      partition.SystemKeyID(),
			Created: sk.Created(),
		}

		// Preload the cache with the system keys
		_, _ = skCache.GetOrLoad(*meta, keyLoaderFunc(func() (*internal.CryptoKey, error) {
			return sk, nil
		}))
	}

	if cacheIK != EMPTY {
		if cacheIK == RETIRED {
			// We create a revoked copy of the same key
			ik = createRevokedKey(ik, secretFactory)
		}

		meta := &KeyMeta{
			ID:      partition.IntermediateKeyID(),
			Created: ik.Created(),
		}

		// Preload the cache with the intermediate keys
		_, _ = ikCache.GetOrLoad(*meta, keyLoaderFunc(func() (*internal.CryptoKey, error) {
			return ik, nil
		}))
	}

	return ikCache, skCache
}

func createMetastoreSpy(ctx context.Context, metaIK, metaSK string, intermediateKey, systemKey *internal.CryptoKey,
	crypto AEAD, partition partition, km KeyManagementService) *spyMetastore {
	metastore := spyMetastore{
		envelopes: make(map[string]map[int64]*EnvelopeKeyRecord),
	}

	metastore.On("Load", mock.Anything, mock.Anything, mock.Anything).Return()
	metastore.On("LoadLatest", mock.Anything, mock.Anything).Return()
	metastore.On("Store", mock.Anything, mock.Anything, mock.Anything, mock.Anything).Return()

	sk := systemKey
	ik := intermediateKey

	if metaSK != EMPTY {
		if metaSK == RETIRED {
			// We create a revoked copy of the same key
			sk = createRevokedKey(sk, secretFactory)
		}

		encKey, _ := internal.WithKeyFunc(sk, func(keyBytes []byte) ([]byte, error) {
			return km.EncryptKey(ctx, keyBytes)
		})

		ekr := &EnvelopeKeyRecord{
			ID:           partition.SystemKeyID(),
			Revoked:      sk.Revoked(),
			Created:      sk.Created(),
			EncryptedKey: encKey,
		}
		metastore.Store(context.Background(), partition.SystemKeyID(), sk.Created(), ekr)
	}

	if metaIK != EMPTY {
		if metaIK == RETIRED {
			// We create a revoked copy of the same key
			ik = createRevokedKey(ik, secretFactory)
		}

		encKey, _ := internal.WithKeyFunc(ik, func(keyBytes []byte) ([]byte, error) {
			return internal.WithKeyFunc(sk, func(systemKeyBytes []byte) ([]byte, error) {
				return crypto.Encrypt(keyBytes, systemKeyBytes)
			})
		})
		ekr := &EnvelopeKeyRecord{
			Revoked:      ik.Revoked(),
			ID:           partition.IntermediateKeyID(),
			Created:      ik.Created(),
			EncryptedKey: encKey,
			ParentKeyMeta: &KeyMeta{
				ID:      partition.SystemKeyID(),
				Created: sk.Created(),
			},
		}

		metastore.Store(context.Background(), partition.IntermediateKeyID(), ik.Created(), ekr)
	}

	metastore.Calls = nil

	return &metastore
}

func createCryptoMock(payload []byte) *MockCrypto {
	crypto := new(MockCrypto)

	// Setup mocking for drr
	crypto.On("Encrypt", payload, mock.Anything).Return([]byte("encryptedPayload"), nil)
	crypto.On("Decrypt", []byte("encryptedPayload"), mock.Anything).Return(payload, nil)
	// Setup mocking for ik and sk
	crypto.On("Encrypt", mock.Anything, mock.Anything).Return([]byte("encryptedKey"), nil)
	crypto.On("Decrypt", []byte("encryptedKey"), mock.Anything).Return([]byte("decryptedKey"), nil)

	return crypto
}

func generatePayload() []byte {
	rand.Seed(time.Now().UnixNano())

	payload := make([]byte, 32)
	for i := range payload {
		payload[i] = letterBytes[rand.Intn(len(letterBytes))]
	}

	return payload
}

func generateTestID(cacheIK, metaIK, cacheSK, metaSK string) (string, string, string) {
	testID := cacheIK + "CacheIK_" + metaIK + "MetastoreIK_" + cacheSK + "CacheSK_" + metaSK + "MetastoreSK"
	partitionID := cacheIK + "CacheIK_" + metaIK + "MetastoreIK_" + strconv.FormatInt(time.Now().Unix(), 10)
	serviceID := cacheSK + "CacheSK_" + metaSK + "MetastoreSK_" + strconv.FormatInt(time.Now().Unix(), 10)

	return testID, partitionID, serviceID
}

func verifyEncryptFlow(t *testing.T, metastoreMock *mock.Mock, states *encryptKeyStates, ikID, skID string) {
	numberOfCalls := 0
	// If IK is stored to metastore
	if states.shouldStoreIK() {
		metastoreMock.AssertCalled(t, "Store", context.Background(), ikID, mock.Anything, mock.Anything)
		numberOfCalls++
	} else {
		metastoreMock.AssertNotCalled(t, "Store", context.Background(), ikID, mock.Anything, mock.Anything)
	}
	// If SK is stored to metastore
	if states.shouldStoreSK() {
		metastoreMock.AssertCalled(t, "Store", context.Background(), skID, mock.Anything, mock.Anything)
		numberOfCalls++
	} else {
		metastoreMock.AssertNotCalled(t, "Store", context.Background(), skID, mock.Anything, mock.Anything)
	}
	// If neither IK nor SK is stored
	if !states.shouldStoreIK() && !states.shouldStoreSK() {
		metastoreMock.AssertNotCalled(t, "Store", mock.Anything, mock.Anything, mock.Anything, mock.Anything)
	}

	metastoreMock.AssertNumberOfCalls(t, "Store", numberOfCalls)

	// NOTE: We do not read IK from the metastore in case of Encrypt
	numberOfCalls = 0
	// If SK is loaded from metastore
	if states.shouldLoadSK() {
		metastoreMock.AssertCalled(t, "Load", context.Background(), skID, mock.Anything)
		numberOfCalls++
	} else {
		metastoreMock.AssertNotCalled(t, "Load", mock.Anything, mock.Anything, mock.Anything)
	}

	metastoreMock.AssertNumberOfCalls(t, "Load", numberOfCalls)

	numberOfCalls = 0
	// If latest IK is loaded from metastore
	if states.shouldLoadLatestIK() {
		metastoreMock.AssertCalled(t, "LoadLatest", context.Background(), ikID)
		numberOfCalls++
	} else {
		metastoreMock.AssertNotCalled(t, "LoadLatest", context.Background(), ikID)
	}
	// If latest SK is loaded from metastore
	if states.shouldLoadLatestSK() {
		metastoreMock.AssertCalled(t, "LoadLatest", context.Background(), skID)
		numberOfCalls++
	} else {
		metastoreMock.AssertNotCalled(t, "LoadLatest", context.Background(), skID)
	}
	// If neither latest IK or SK is loaded from metastore
	if !states.shouldLoadLatestSK() && !states.shouldLoadLatestIK() {
		metastoreMock.AssertNotCalled(t, "LoadLatest", mock.Anything, mock.Anything)
	}

	metastoreMock.AssertNumberOfCalls(t, "LoadLatest", numberOfCalls)
}

func verifyDecryptFlow(t *testing.T, metastoreMock *mock.Mock, states *decryptKeyStates, ikID, skID string) {
	// If IK is loaded from metastore
	if states.shouldLoadIK() {
		metastoreMock.AssertCalled(t, "Load", context.Background(), ikID, mock.Anything)
	}

	// If SK is loaded from metastore
	if states.shouldLoadSK() {
		metastoreMock.AssertCalled(t, "Load", context.Background(), skID, mock.Anything)
	}
}

type encryptKeyStates struct {
	cacheIK, metaIK, cacheSK, metaSK string
}

func (s encryptKeyStates) shouldStoreIK() bool {
	if s.cacheIK == VALID {
		return false
	}

	if s.metaIK != VALID {
		return true
	}

	// At this stage IK is valid in metastore
	// The existing IK can only be used if the SK is valid in cache
	// or if the SK is missing from the cache but is valid in metastore
	if s.cacheSK == VALID {
		return false
	}

	if s.cacheSK == EMPTY {
		if s.metaSK == VALID {
			return false
		}
	}

	return true
}

func (s encryptKeyStates) shouldStoreSK() bool {
	if s.cacheIK == VALID {
		return false
	}

	return s.cacheSK != VALID && s.metaSK != VALID
}

func (s encryptKeyStates) shouldLoadSK() bool {
	if s.cacheIK == VALID {
		return false
	}

	if s.metaIK != VALID {
		return false
	}

	if s.cacheSK == EMPTY {
		return true
	}

	return false
}

func (s encryptKeyStates) shouldLoadLatestIK() bool {
	return s.cacheIK != VALID
}

func (s encryptKeyStates) shouldLoadLatestSK() bool {
	if s.cacheIK == VALID {
		return false
	}

	if s.metaIK == VALID {
		// Since the cache SK is retired, we create a new IK.
		// Because the cache SK happens to be the latest one in cache,
		// we need to load the latest SK from metastore during IK creation flow
		if s.cacheSK == RETIRED {
			return true
		}

		// Since the SK is not in the cache and not valid in the metastore, we have to create a new IK.
		// This requires loading the latest SK from metastore during IK creation flow
		return s.cacheSK == EMPTY && s.metaSK != VALID
	}

	return s.cacheSK != VALID
}

type decryptKeyStates struct {
	cacheIK, cacheSK string
}

func (s decryptKeyStates) shouldLoadIK() bool {
	return s.cacheIK == EMPTY
}

func (s decryptKeyStates) shouldLoadSK() bool {
	if s.shouldLoadIK() {
		return s.cacheSK == EMPTY
	}

	return false
}
