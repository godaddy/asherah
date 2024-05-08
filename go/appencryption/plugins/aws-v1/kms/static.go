package kms

import (
	"context"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/pkg/errors"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
)

var _ appencryption.KeyManagementService = (*StaticKMS)(nil)

const staticKMSKeySize = 32

// StaticKMS is an in-memory static implementation of a KeyManagementService.
// NOTE: It should not be used in production and is for testing only!
type StaticKMS struct {
	Crypto appencryption.AEAD
	key    *internal.CryptoKey
}

// NewStatic constructs a new StaticKMS. The provided key MUST be
// be 32 bytes in length.
func NewStatic(key string, crypto appencryption.AEAD) (*StaticKMS, error) {
	if len(key) != staticKMSKeySize {
		return nil, errors.Errorf("invalid key size %d, must be 32 bytes", len(key))
	}

	// just hard-code the internal one being used
	f := new(memguard.SecretFactory)

	cryptoKey, err := internal.NewCryptoKey(f, time.Now().Unix(), false, []byte(key))
	if err != nil {
		return nil, err
	}

	return &StaticKMS{
		Crypto: crypto,
		key:    cryptoKey,
	}, nil
}

// EncryptKey takes in an unencrypted byte slice and encrypts it with the master key.
// The returned value should then be inserted into the Metastore before being
// used.
func (s *StaticKMS) EncryptKey(_ context.Context, bytes []byte) ([]byte, error) {
	dst, err := internal.WithKeyFunc(s.key, func(keyBytes []byte) ([]byte, error) {
		return s.Crypto.Encrypt(bytes, keyBytes)
	})

	if err != nil {
		return nil, err
	}

	return dst, nil
}

// DecryptKey decrypts the encrypted byte slice using the master key.
func (s *StaticKMS) DecryptKey(ctx context.Context, encKey []byte) ([]byte, error) {
	keyBytes, err := internal.WithKeyFunc(s.key, func(kekBytes []byte) ([]byte, error) {
		return s.Crypto.Decrypt(encKey, kekBytes)
	})

	if err != nil {
		return nil, err
	}

	return keyBytes, nil
}

// Close frees the memory locked by the static key. It should be called
// as soon as its no longer in use.
func (s *StaticKMS) Close() {
	if s.key != nil {
		s.key.Close()
	}
}
