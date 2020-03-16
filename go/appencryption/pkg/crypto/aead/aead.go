package aead

import (
	"crypto/cipher"

	"github.com/pkg/errors"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

type cryptoFunc func(key []byte) (cipher.AEAD, error)

// EncryptKey encrypts data using the provided key bytes.
func (c cryptoFunc) Encrypt(data, encKey []byte) ([]byte, error) {
	aeadCipher, err := c(encKey)
	if err != nil {
		return nil, err
	}

	cipherAndNonce := make([]byte, len(data)+aeadCipher.Overhead()+aeadCipher.NonceSize())
	noncePos := len(cipherAndNonce) - aeadCipher.NonceSize()

	internal.FillRandom(cipherAndNonce[noncePos:])

	aeadCipher.Seal(cipherAndNonce[:0], cipherAndNonce[noncePos:], data, nil)

	return cipherAndNonce, nil
}

// DecryptKey decrypts data using the provided key.
func (c cryptoFunc) Decrypt(data, key []byte) ([]byte, error) {
	aeadCipher, err := c(key)
	if err != nil {
		return nil, err
	}

	if len(data) < aeadCipher.NonceSize() {
		return nil, errors.New("data length is shorter than nonce size")
	}

	noncePos := len(data) - aeadCipher.NonceSize()

	d, err := aeadCipher.Open(nil, data[noncePos:], data[:noncePos], nil)

	return d, errors.Wrap(err, "error decrypting data")
}
