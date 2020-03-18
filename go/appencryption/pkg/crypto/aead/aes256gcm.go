package aead

import (
	"crypto/aes"
	"crypto/cipher"

	"github.com/godaddy/asherah/go/appencryption"
)

// aesGCMCipherFactory returns a AEAD cipher using AES/GCM
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
