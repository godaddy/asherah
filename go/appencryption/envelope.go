package appencryption

import (
	"context"
	"fmt"
	"sync"
	"time"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

// MetricsPrefix prefixes all metrics names
const MetricsPrefix = "ael"

// Envelope metrics
var (
	decryptTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.drr.decrypt", MetricsPrefix), nil)
	encryptTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.drr.encrypt", MetricsPrefix), nil)
)

// KeyMeta contains the ID and Created timestamp for an encryption key.
type KeyMeta struct {
	ID      string `json:"KeyId"`
	Created int64  `json:"Created"`
}

// String returns a string with the KeyMeta values.
func (m KeyMeta) String() string {
	return fmt.Sprintf("KeyMeta [keyId=%s created=%d]", m.ID, m.Created)
}

// DataRowRecord contains the encrypted key and provided data, as well as the information
// required to decrypt the key encryption key. This struct should be stored in your
// data persistence as it's required to decrypt data.
type DataRowRecord struct {
	Key  *EnvelopeKeyRecord
	Data []byte
}

// EnvelopeKeyRecord represents an encrypted key and is the data structure used
// to persist the key in our key table. It also contains the meta data
// of the key used to encrypt it.
type EnvelopeKeyRecord struct {
	Revoked       bool     `json:"Revoked,omitempty"`
	ID            string   `json:"-"`
	Created       int64    `json:"Created"`
	EncryptedKey  []byte   `json:"Key"`
	ParentKeyMeta *KeyMeta `json:"ParentKeyMeta,omitempty"`
}

// Verify envelopeEncryption implements the Encryption interface.
var _ Encryption = (*envelopeEncryption)(nil)

// envelopeEncryption is used to encrypt and decrypt data related to a specific partition ID.
type envelopeEncryption struct {
	partition        partition
	Metastore        Metastore
	KMS              KeyManagementService
	Policy           *CryptoPolicy
	Crypto           AEAD
	SecretFactory    securememory.SecretFactory
	systemKeys       cache
	intermediateKeys cache
}

// loadSystemKey fetches a known system key from the metastore and decrypts it using the key management service.
func (e *envelopeEncryption) loadSystemKey(ctx context.Context, meta KeyMeta) (*internal.CryptoKey, error) {
	ekr, err := e.Metastore.Load(ctx, meta.ID, meta.Created)
	if err != nil {
		return nil, err
	}

	if ekr == nil {
		return nil, errors.New("error loading system key from metastore")
	}

	return e.systemKeyFromEKR(ctx, ekr)
}

// systemKeyFromEKR decrypts ekr using the key management service and returns a new CryptoKey containing the decrypted key data.
func (e *envelopeEncryption) systemKeyFromEKR(ctx context.Context, ekr *EnvelopeKeyRecord) (*internal.CryptoKey, error) {
	bytes, err := e.KMS.DecryptKey(ctx, ekr.EncryptedKey)
	if err != nil {
		return nil, err
	}

	return internal.NewCryptoKey(e.SecretFactory, ekr.Created, ekr.Revoked, bytes)
}

// intermediateKeyFromEKR decrypts ekr using sk and returns a new CryptoKey containing the decrypted key data.
func (e *envelopeEncryption) intermediateKeyFromEKR(sk *internal.CryptoKey, ekr *EnvelopeKeyRecord) (*internal.CryptoKey, error) {
	if ekr != nil && ekr.ParentKeyMeta != nil && sk.Created() != ekr.ParentKeyMeta.Created {
		//In this case, the system key just rotated and this EKR was encrypted with the prior SK.
		//A duplicate IK would have been attempted to create with the correct SK but would create a duplicate so is discarded.
		//Lookup the correct system key so the ik decryption can succeed.
		skLoaded, err := e.getOrLoadSystemKey(context.Background(), *ekr.ParentKeyMeta)
		if err != nil {
			return nil, err
		}

		sk = skLoaded
	}

	ikBuffer, err := internal.WithKeyFunc(sk, func(skBytes []byte) ([]byte, error) {
		return e.Crypto.Decrypt(ekr.EncryptedKey, skBytes)
	})
	if err != nil {
		return nil, err
	}

	return internal.NewCryptoKey(e.SecretFactory, ekr.Created, ekr.Revoked, ikBuffer)
}

// loadLatestOrCreateSystemKey gets the most recently created system key for the given id or creates a new one.
func (e *envelopeEncryption) loadLatestOrCreateSystemKey(ctx context.Context, id string) (*internal.CryptoKey, error) {
	ekr, err := e.Metastore.LoadLatest(ctx, id)
	if err != nil {
		return nil, err
	}

	if ekr != nil && !e.isEnvelopeInvalid(ekr) {
		// We've retrieved an SK from the store and it's valid, use it.
		return e.systemKeyFromEKR(ctx, ekr)
	}

	// Create a new SK
	sk, err := e.generateKey()
	if err != nil {
		return nil, err
	}

	switch success, err2 := e.tryStoreSystemKey(ctx, sk); {
	case success:
		// New key saved successfully, return it.
		return sk, nil
	default:
		// it's no good to us now. throw it away
		sk.Close()

		if err2 != nil {
			return nil, err2
		}
	}

	// Well, we created a new SK but the store was unsuccessful. Let's assume that's because we
	// attempted to save a duplicate key to the metastore because, if that's the case a new key
	// was added sometime between our load and store attempts, so let's grab it and return it.
	ekr, err = e.mustLoadLatest(ctx, id)
	if err != nil {
		// Oops! Looks like our assumption was incorrect and all we can do now is report the error.
		return nil, err
	}

	return e.systemKeyFromEKR(ctx, ekr)
}

// tryStoreSystemKey attempts to persist the encrypted sk to the metastore ignoring all persistence related errors.
// err will be non-nil only if encryption fails.
func (e *envelopeEncryption) tryStoreSystemKey(ctx context.Context, sk *internal.CryptoKey) (success bool, err error) {
	encKey, err := internal.WithKeyFunc(sk, func(keyBytes []byte) ([]byte, error) {
		return e.KMS.EncryptKey(ctx, keyBytes)
	})
	if err != nil {
		return false, err
	}

	ekr := &EnvelopeKeyRecord{
		ID:           e.partition.SystemKeyID(),
		Created:      sk.Created(),
		EncryptedKey: encKey,
	}

	return e.tryStore(ctx, ekr), nil
}

var _ keyReloader = (*reloader)(nil)

type reloader struct {
	loadedKeys    []*internal.CryptoKey
	mu            sync.Mutex
	loader        keyLoader
	isInvalidFunc func(key *internal.CryptoKey) bool
	keyID         string
	isCached      bool
}

// Load implements keyLoader.
func (r *reloader) Load() (*internal.CryptoKey, error) {
	k, err := r.loader.Load()
	if err != nil {
		return nil, err
	}

	r.append(k)

	return k, nil
}

// append a key to the list of loaded keys. A call to
// Close will close all appended keys.
func (r *reloader) append(key *internal.CryptoKey) {
	r.mu.Lock()
	r.loadedKeys = append(r.loadedKeys, key)
	r.mu.Unlock()
}

// IsInvalid implements keyReloader
func (r *reloader) IsInvalid(key *internal.CryptoKey) bool {
	return r.isInvalidFunc(key)
}

// Close calls maybeCloseKey for all keys previously loaded by a reloader instance.
func (r *reloader) Close() {
	r.mu.Lock()
	defer r.mu.Unlock()

	for k := range r.loadedKeys {
		key := r.loadedKeys[k]

		maybeCloseKey(r.isCached, key)
	}
}

// GetOrLoadLatest wraps the GetOrLoadLatest of c using r as the loader.
func (r *reloader) GetOrLoadLatest(c cache) (*internal.CryptoKey, error) {
	return c.GetOrLoadLatest(r.keyID, r)
}

// newIntermediateKeyReloader returns a new reloader for intermediate keys.
func (e *envelopeEncryption) newIntermediateKeyReloader(ctx context.Context) *reloader {
	return e.newKeyReloader(
		ctx,
		e.partition.IntermediateKeyID(),
		e.Policy.CacheIntermediateKeys,
		e.loadLatestOrCreateIntermediateKey,
	)
}

// newSystemKeyReloader returns a new reloader for system keys.
func (e *envelopeEncryption) newSystemKeyReloader(ctx context.Context) *reloader {
	return e.newKeyReloader(
		ctx,
		e.partition.SystemKeyID(),
		e.Policy.CacheSystemKeys,
		e.loadLatestOrCreateSystemKey,
	)
}

// newKeyReloader returns a new reloader.
func (e *envelopeEncryption) newKeyReloader(
	ctx context.Context,
	id string,
	isCached bool,
	loader func(context.Context, string) (*internal.CryptoKey, error),
) *reloader {
	return &reloader{
		keyID:    id,
		isCached: isCached,
		loader: keyLoaderFunc(func() (*internal.CryptoKey, error) {
			return loader(ctx, id)
		}),
		isInvalidFunc: e.isKeyInvalid,
	}
}

// isKeyInvalid checks if the key is revoked or expired.
func (e *envelopeEncryption) isKeyInvalid(key *internal.CryptoKey) bool {
	return key.Revoked() || isKeyExpired(key.Created(), e.Policy.ExpireKeyAfter)
}

// isEnvelopeInvalid checks if the envelope key record is revoked or has an expired key.
func (e *envelopeEncryption) isEnvelopeInvalid(ekr *EnvelopeKeyRecord) bool {
	// TODO Add key rotation policy check. If not inline, then can return valid even if expired
	return e == nil || isKeyExpired(ekr.Created, e.Policy.ExpireKeyAfter) || ekr.Revoked
}

func (e *envelopeEncryption) generateKey() (*internal.CryptoKey, error) {
	createdAt := newKeyTimestamp(e.Policy.CreateDatePrecision)
	return internal.GenerateKey(e.SecretFactory, createdAt, AES256KeySize)
}

// tryStore attempts to persist the key into the metastore and returns true if successful.
//
// SQL metastore has a limitation where we can't detect duplicate key from other errors,
// so we treat all errors as if they are duplicates. If it's a systemic issue, it
// likely would've already occurred in the previous lookup (or in the next one). Worst case is a
// caller has to retry, which they would have needed to anyway.
func (e *envelopeEncryption) tryStore(
	ctx context.Context,
	ekr *EnvelopeKeyRecord,
) bool {
	success, err := e.Metastore.Store(ctx, ekr.ID, ekr.Created, ekr)

	_ = err // err is intentionally ignored

	return success
}

// mustLoadLatest attempts to load the latest key from the metastore and returns an error if no key is found
// matching id.
func (e *envelopeEncryption) mustLoadLatest(ctx context.Context, id string) (*EnvelopeKeyRecord, error) {
	ekr, err := e.Metastore.LoadLatest(ctx, id)
	if err != nil {
		return nil, err
	}

	if ekr == nil {
		return nil, errors.New("error loading key from metastore after retry")
	}

	return ekr, nil
}

// createIntermediateKey creates a new IK and attempts to persist the new key to the metastore.
// If unsuccessful createIntermediateKey will attempt to fetch the latest IK from the metastore.
func (e *envelopeEncryption) createIntermediateKey(ctx context.Context) (*internal.CryptoKey, error) {
	r := e.newSystemKeyReloader(ctx)
	defer r.Close()

	// Try to get latest from cache.
	sk, err := r.GetOrLoadLatest(e.systemKeys)
	if err != nil {
		return nil, err
	}

	ik, err := e.generateKey()
	if err != nil {
		return nil, err
	}

	switch success, err2 := e.tryStoreIntermediateKey(ctx, ik, sk); {
	case success:
		// New key saved successfully, return it.
		return ik, nil
	default:
		// it's no good to us now. throw it away
		ik.Close()

		if err2 != nil {
			return nil, err2
		}
	}

	// If we're here, storing of the newly generated key failed above which means we attempted to
	// save a duplicate key to the metastore. If that's the case, then we know a valid key exists
	// in the metastore, so let's grab it and return it.
	newEkr, err := e.mustLoadLatest(ctx, e.partition.IntermediateKeyID())
	if err != nil {
		return nil, err
	}

	return e.intermediateKeyFromEKR(sk, newEkr)
}

// tryStoreIntermediateKey attempts to persist the encrypted ik to the metastore ignoring all persistence related errors.
// err will be non-nil only if encryption fails.
func (e *envelopeEncryption) tryStoreIntermediateKey(ctx context.Context, ik, sk *internal.CryptoKey) (success bool, err error) {
	encBytes, err := internal.WithKeyFunc(ik, func(keyBytes []byte) ([]byte, error) {
		return internal.WithKeyFunc(sk, func(systemKeyBytes []byte) ([]byte, error) {
			return e.Crypto.Encrypt(keyBytes, systemKeyBytes)
		})
	})
	if err != nil {
		return false, err
	}

	ekr := &EnvelopeKeyRecord{
		ID:           e.partition.IntermediateKeyID(),
		Created:      ik.Created(),
		EncryptedKey: encBytes,
		ParentKeyMeta: &KeyMeta{
			ID:      e.partition.SystemKeyID(),
			Created: sk.Created(),
		},
	}

	return e.tryStore(ctx, ekr), nil
}

// loadLatestOrCreateIntermediateKey gets the most recently created intermediate key for the given id or creates a new one.
func (e *envelopeEncryption) loadLatestOrCreateIntermediateKey(ctx context.Context, id string) (*internal.CryptoKey, error) {
	ikEkr, err := e.Metastore.LoadLatest(ctx, id)
	if err != nil {
		return nil, err
	}

	if ikEkr == nil || e.isEnvelopeInvalid(ikEkr) {
		return e.createIntermediateKey(ctx)
	}

	// We've retrieved the latest IK and confirmed its validity. Now let's do the same for its parent key.
	sk, err := e.getOrLoadSystemKey(ctx, *ikEkr.ParentKeyMeta)
	if err != nil {
		return e.createIntermediateKey(ctx)
	}

	defer maybeCloseKey(e.Policy.CacheSystemKeys, sk)

	// Only use the loaded IK if it and its parent key is valid.
	if ik := e.getValidIntermediateKey(sk, ikEkr); ik != nil {
		return ik, nil
	}

	// fallback to creating a new one
	return e.createIntermediateKey(ctx)
}

// getOrLoadSystemKey returns a system key from cache if it's already been loaded. Otherwise it retrieves the key
// from the metastore.
func (e *envelopeEncryption) getOrLoadSystemKey(ctx context.Context, meta KeyMeta) (*internal.CryptoKey, error) {
	loader := keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return e.loadSystemKey(ctx, meta)
	})

	return e.systemKeys.GetOrLoad(meta, loader)
}

// getValidIntermediateKey returns a new CryptoKey constructed from ekr. It returns nil if sk is invalid or if key initialization fails.
func (e *envelopeEncryption) getValidIntermediateKey(sk *internal.CryptoKey, ekr *EnvelopeKeyRecord) *internal.CryptoKey {
	// IK is only valid if its parent is valid
	if e.isKeyInvalid(sk) {
		return nil
	}

	ik, err := e.intermediateKeyFromEKR(sk, ekr)
	if err != nil {
		return nil
	}

	// all is well with the loaded IK, use it
	return ik
}

// decryptRow decrypts drr using ik as the parent key and returns the decrypted data.
func decryptRow(ik *internal.CryptoKey, drr DataRowRecord, crypto AEAD) ([]byte, error) {
	return internal.WithKeyFunc(ik, func(bytes []byte) ([]byte, error) {
		// TODO Consider having separate DecryptKey that is functional and handles wiping bytes
		rawDrk, err := crypto.Decrypt(drr.Key.EncryptedKey, bytes)
		if err != nil {
			return nil, err
		}

		defer internal.MemClr(rawDrk)

		return crypto.Decrypt(drr.Data, rawDrk)
	})
}

// maybeCloseKey closes key if isCached is false.
func maybeCloseKey(isCached bool, key *internal.CryptoKey) {
	if !isCached {
		key.Close()
	}
}

// EncryptPayload encrypts a provided slice of bytes and returns the data with the data row key and required
// parent information to decrypt the data in the future. It also takes a context used for cancellation.
func (e *envelopeEncryption) EncryptPayload(ctx context.Context, data []byte) (*DataRowRecord, error) {
	defer encryptTimer.UpdateSince(time.Now())

	reloader := e.newIntermediateKeyReloader(ctx)
	defer reloader.Close()

	// Try to get latest from cache.
	ik, err := reloader.GetOrLoadLatest(e.intermediateKeys)
	if err != nil {
		return nil, err
	}

	// Note the id doesn't mean anything for DRK. Don't need to truncate created since that is intended
	// to prevent excessive IK/SK creation (we always create new DRK on each write, so not a concern there)
	drk, err := internal.GenerateKey(e.SecretFactory, time.Now().Unix(), AES256KeySize)
	if err != nil {
		return nil, err
	}

	defer drk.Close()

	encData, err := internal.WithKeyFunc(drk, func(bytes []byte) ([]byte, error) {
		return e.Crypto.Encrypt(data, bytes)
	})
	if err != nil {
		return nil, err
	}

	encBytes, err := internal.WithKeyFunc(ik, func(bytes []byte) ([]byte, error) {
		return internal.WithKeyFunc(drk, func(drkBytes []byte) ([]byte, error) {
			return e.Crypto.Encrypt(drkBytes, bytes)
		})
	})
	if err != nil {
		return nil, err
	}

	return &DataRowRecord{
		Key: &EnvelopeKeyRecord{
			Created:      drk.Created(),
			EncryptedKey: encBytes,
			ParentKeyMeta: &KeyMeta{
				Created: ik.Created(),
				ID:      e.partition.IntermediateKeyID(),
			},
		},
		Data: encData,
	}, nil
}

// DecryptDataRowRecord decrypts a DataRowRecord key and returns the original byte slice provided to the encrypt function.
// It also accepts a context for cancellation.
func (e *envelopeEncryption) DecryptDataRowRecord(ctx context.Context, drr DataRowRecord) ([]byte, error) {
	defer decryptTimer.UpdateSince(time.Now())

	if drr.Key == nil {
		return nil, errors.New("datarow key record cannot be empty")
	}

	if drr.Key.ParentKeyMeta == nil {
		return nil, errors.New("parent key cannot be empty")
	}

	if !e.partition.IsValidIntermediateKeyID(drr.Key.ParentKeyMeta.ID) {
		return nil, errors.New("unable to decrypt record")
	}

	loader := keyLoaderFunc(func() (*internal.CryptoKey, error) {
		return e.loadIntermediateKey(ctx, *drr.Key.ParentKeyMeta)
	})

	ik, err := e.intermediateKeys.GetOrLoad(*drr.Key.ParentKeyMeta, loader)
	if err != nil {
		return nil, err
	}

	defer maybeCloseKey(e.Policy.CacheIntermediateKeys, ik)

	return decryptRow(ik, drr, e.Crypto)
}

// loadIntermediateKey fetches a known intermediate key from the metastore and tries to decrypt it using the associated system key.
func (e *envelopeEncryption) loadIntermediateKey(ctx context.Context, meta KeyMeta) (*internal.CryptoKey, error) {
	ekr, err := e.Metastore.Load(ctx, meta.ID, meta.Created)
	if err != nil {
		return nil, err
	}

	if ekr == nil {
		return nil, errors.New("error loading intermediate key from metastore")
	}

	sk, err := e.getOrLoadSystemKey(ctx, *ekr.ParentKeyMeta)
	if err != nil {
		return nil, err
	}

	defer maybeCloseKey(e.Policy.CacheSystemKeys, sk)

	return e.intermediateKeyFromEKR(sk, ekr)
}

// Close frees all memory locked by the keys in the session. It should be called
// as soon as its no longer in use.
func (e *envelopeEncryption) Close() error {
	return e.intermediateKeys.Close()
}
