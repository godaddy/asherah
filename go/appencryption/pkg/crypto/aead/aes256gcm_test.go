package aead

import (
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/securememory/memguard"
)

var (
	aes256GCMCrypto = NewAES256GCM()
	secretFactory   = new(memguard.SecretFactory)
)

func Test_AESCipherFactory(t *testing.T) {
	c, err := aesGCMCipherFactory(make([]byte, appencryption.AES256KeySize))
	assert.NoError(t, err)
	assert.NotNil(t, c)

	// ensure we're using the standard gcm nonce size of 12
	assert.Equal(t, gcmNonceSize, c.NonceSize())

	// Couldn't figure out how to access interface/struct method for this, but GCM uses 128-bit blocks
	assert.Equal(t, gcmTagSize, c.Overhead())
}

func Test_AESCipherFactory_InvalidKeyLength(t *testing.T) {
	c, err := aesGCMCipherFactory(make([]byte, appencryption.AES256KeySize-1))
	if assert.Error(t, err) {
		assert.Nil(t, c)
	}
}

func Test_AESCipherFactory_Decrypt_DataLessThanNonceSize(t *testing.T) {
	key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
	assert.NoError(t, err)

	defer key.Close()

	res, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Decrypt(make([]byte, 1), keyBytes)
	})
	assert.Error(t, err)
	assert.Nil(t, res)
}

func TestAES256GCM_EncryptDecryptKey(t *testing.T) {
	payload := []byte("some secret string")

	key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
	assert.NoError(t, err)

	defer key.Close()

	encBytes, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Encrypt(payload, keyBytes)
	})
	assert.NoError(t, err)

	decBytes, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Decrypt(encBytes, keyBytes)
	})
	assert.NoError(t, err)

	assert.Equal(t, payload, decBytes)
}

func TestAES256GCM_EncryptTooLargePayload(t *testing.T) {
	// This test is skipped as the memory requirements are too great for our current CI workflows
	// Temporarily remove the following for local testing, if desired.
	t.SkipNow()

	data := []byte("some secret string")

	payload := make([]byte, gcmMaxDataSize+1)

	dataPosition := len(payload) - len(data)
	copy(payload[dataPosition:], data)

	key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
	assert.NoError(t, err)

	defer key.Close()

	_, err = internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Encrypt(payload, keyBytes)
	})
	assert.ErrorContains(t, err, "data too large for GCM")
}

func TestAES256GCM_EncryptDecryptMaxPayload(t *testing.T) {
	// This test is skipped due to its excessive memory requirements (>100GB).
	// Temporarily remove the following for local testing, if desired.
	t.SkipNow()

	data := []byte("some secret string")
	payload := make([]byte, gcmMaxDataSize)

	dataPosition := len(payload) - len(data)
	copy(payload[dataPosition:], data)

	key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
	assert.NoError(t, err)

	defer key.Close()

	encBytes, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Encrypt(payload, keyBytes)
	})
	assert.NoError(t, err)

	payload = nil

	decBytes, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
		return aes256GCMCrypto.Decrypt(encBytes, keyBytes)
	})
	assert.NoError(t, err)

	assert.Equal(t, data, decBytes[dataPosition:])
}

func TestAES256GCM_EncryptDecryptKey_VerifyOutputSize(t *testing.T) {
	key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
	assert.NoError(t, err)

	defer key.Close()

	var (
		blockSize     int
		nonceByteSize int
	)

	err = internal.WithKey(key, func(keyBytes []byte) error {
		aead, _ := aesGCMCipherFactory(keyBytes)
		blockSize = aead.Overhead()
		nonceByteSize = aead.NonceSize()
		return nil
	})
	assert.NoError(t, err)

	for i := 1; i < 4096; i++ {
		payload := make([]byte, i)

		encBytes, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
			return aes256GCMCrypto.Encrypt(payload, keyBytes)
		})
		assert.NoError(t, err)
		assert.Equal(t, i+blockSize+nonceByteSize, len(encBytes))
	}
}
