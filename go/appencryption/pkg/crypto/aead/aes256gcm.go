package aead

import (
	"crypto/aes"
	"crypto/cipher"

	"github.com/godaddy/asherah/go/appencryption"
)

const (
	gcmBlockSize = aes.BlockSize
	gcmNonceSize = 12
	gcmTagSize   = 16

	// gcmMaxDataSize is the maximum message size supported by GCM.
	// See: https://cs.opensource.google/go/go/+/refs/tags/go1.21.0:src/crypto/cipher/gcm.go;l=172
	gcmMaxDataSize = ((1 << 32) - 2) * gcmBlockSize
)

// aesGCMCipherFactory returns a AEAD cipher using AES/GCM.
func aesGCMCipherFactory(key []byte) (cipher.AEAD, error) {
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, err
	}

	return cipher.NewGCM(block)
}

// NewAES256GCM returns the logic required to encrypt data using AES/GCM.
func NewAES256GCM() appencryption.AEAD {
	return cryptoFunc(aesGCMCipherFactory)
}
