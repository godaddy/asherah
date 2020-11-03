package internal

import (
	"fmt"
	"sync"
	"sync/atomic"

	"github.com/godaddy/asherah/go/securememory"
)

// CryptoKey represents an unencrypted key stored in a secure section in memory.
type CryptoKey struct {
	created int64
	secret  securememory.Secret
	once    sync.Once
	revoked uint32
}

// Created returns the time the CryptoKey was created as a Unix epoch in seconds.
func (k *CryptoKey) Created() int64 {
	return k.created
}

// Revoked returns whether the CryptoKey has been marked as revoked or not.
func (k *CryptoKey) Revoked() bool {
	return atomic.LoadUint32(&k.revoked) == 1
}

// SetRevoked atomically sets the revoked flag of the CryptoKey to the given value.
func (k *CryptoKey) SetRevoked(revoked bool) {
	var revokedInt uint32
	if revoked {
		revokedInt = 1
	}

	atomic.StoreUint32(&k.revoked, revokedInt)
}

// Close destroys the underlying buffer for this key.
func (k *CryptoKey) Close() {
	k.once.Do(k.close)
}

// Close destroys the underlying buffer for this key.
func (k *CryptoKey) close() {
	k.secret.Close()
}

// IsClosed returns true if the underlying buffer has been closed.
func (k *CryptoKey) IsClosed() bool {
	return k.secret.IsClosed()
}

func (k *CryptoKey) String() string {
	return fmt.Sprintf("CryptoKey(%p){secret(%p)}", k, k.secret)
}

// NewCryptoKey creates a CryptoKey using the given key. Note that the underlying array will be wiped after the function
// exits.
func NewCryptoKey(factory securememory.SecretFactory, created int64, revoked bool, key []byte) (*CryptoKey, error) {
	var revokedInt uint32
	if revoked {
		revokedInt = 1
	}

	sec, err := factory.New(key)
	if err != nil {
		return nil, err
	}

	return &CryptoKey{
		created: created,
		revoked: revokedInt,
		secret:  sec,
	}, nil
}

// NewCryptoKeyForTest creates a CryptoKey intended to be used for TEST only.
// TODO: explore refactoring dependent tests to eliminate the need for this function.
func NewCryptoKeyForTest(created int64, revoked bool) *CryptoKey {
	var revokedInt uint32
	if revoked {
		revokedInt = 1
	}

	return &CryptoKey{
		created: created,
		revoked: revokedInt,
		secret:  nil,
	}
}

// GenerateKey creates a new random CryptoKey.
func GenerateKey(factory securememory.SecretFactory, created int64, size int) (*CryptoKey, error) {
	sec, err := factory.CreateRandom(size)
	if err != nil {
		return nil, err
	}

	return &CryptoKey{
		created: created,
		revoked: 0,
		secret:  sec,
	}, nil
}

// WithKey takes in a CryptoKey, makes the underlying bytes readable, and passes them to the function provided.
// A reference MUST not be stored to the provided bytes. The underlying array will be wiped after the function
// exits.
func WithKey(key *CryptoKey, action func([]byte) error) error {
	return key.secret.WithBytes(action)
}

// WithKeyFunc takes in a CryptoKey, makes the underlying bytes readable, and passes them to the function provided.
// A reference MUST not be stored to the provided bytes. The underlying array will be wiped after the function
// exits.
func WithKeyFunc(key *CryptoKey, action func([]byte) ([]byte, error)) ([]byte, error) {
	return key.secret.WithBytesFunc(action)
}
