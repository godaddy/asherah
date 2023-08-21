package aead

import (
	"crypto/cipher"

	"github.com/pkg/errors"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

type cryptoFunc func(key []byte) (cipher.AEAD, error)

// Encrypt encrypts data using the provided key bytes.
func (c cryptoFunc) Encrypt(data, encKey []byte) ([]byte, error) {
	aeadCipher, err := c(encKey)
	if err != nil {
		return nil, err
	}

	if len(data) > gcmMaxDataSize {
		return nil, errors.New("data too large for GCM")
	}

	if gcmTagSize != aeadCipher.Overhead() {
		return nil, errors.New("unexpected cipher overhead")
	}

	if gcmNonceSize != aeadCipher.NonceSize() {
		return nil, errors.New("unexpected cipher nonce size")
	}

	size := len(data) + gcmTagSize + gcmNonceSize

	cipherAndNonce := make([]byte, size)
	noncePos := len(cipherAndNonce) - aeadCipher.NonceSize()

	internal.FillRandom(cipherAndNonce[noncePos:])

	aeadCipher.Seal(cipherAndNonce[:0], cipherAndNonce[noncePos:], data, nil)

	return cipherAndNonce, nil
}

// Decrypt decrypts data using the provided key.
func (c cryptoFunc) Decrypt(data, key []byte) ([]byte, error) {
	aeadCipher, err := c(key)
	if err != nil {
		return nil, err
	}

	if len(data) < aeadCipher.NonceSize() {
		return nil, errors.New("data length is shorter than nonce size")
	}

	noncePos := len(data) - aeadCipher.NonceSize()

	// Unfortunately we can't reuse ciphertext's storage here (ie the data slice)
	// as we don't control the its lifecycle. For instance, in the case of DEKs
	// and KEKs this storage is wiped immediately after calling this function.
	d, err := aeadCipher.Open(nil, data[noncePos:], data[:noncePos], nil)

	return d, errors.Wrap(err, "error decrypting data")
}
