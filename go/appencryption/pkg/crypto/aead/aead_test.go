package aead

import (
	"crypto/cipher"
	"errors"
	"testing"

	"github.com/stretchr/testify/assert"
)

func TestCrypto_Encrypt_CipherFactoryReturnsError(t *testing.T) {
	c := cryptoFunc(func(_ []byte) (cipher.AEAD, error) {
		return nil, errors.New("error creating cipher")
	})

	b, err := c.Encrypt(nil, nil)
	if assert.Error(t, err) {
		assert.Nil(t, b)
	}
}

func TestCrypto_Decrypt_CipherFactoryReturnsError(t *testing.T) {
	c := cryptoFunc(func(_ []byte) (cipher.AEAD, error) {
		return nil, errors.New("error creating cipher")
	})

	b, err := c.Decrypt([]byte("jhhjfdjksahfkdjsafhdajslhfdjksalhfkjhdsakfjhsdaklfdsa"), nil)
	if assert.Error(t, err) {
		assert.Nil(t, b)
	}
}
