package kms

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
)

var (
	genericErrorMessage = "generic error message"

	secretFactory = new(memguard.SecretFactory)
)

type MockCrypto struct {
	mock.Mock
}

func (c *MockCrypto) Encrypt(data, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (c *MockCrypto) Decrypt(data []byte, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func TestStaticKMS_Encrypt(t *testing.T) {
	crypto := aead.NewAES256GCM()
	m, err := NewStatic("bbsPfQTZsmwEcSRKND87WpoC9umuuuOo", crypto)

	if assert.NoError(t, err) {
		key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), appencryption.AES256KeySize)
		require.NoError(t, err)

		encKey, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
			return m.EncryptKey(context.Background(), keyBytes)
		})
		require.NoError(t, err)

		afterBytes, err := m.DecryptKey(context.Background(), encKey)
		if assert.NoError(t, err) {
			err := internal.WithKey(key, func(beforeBytes []byte) error {
				assert.Equal(t, beforeBytes, afterBytes)
				return nil
			})
			assert.NoError(t, err)
		}
	}
}

func TestStaticKMS_EncryptKey_ReturnsErrorOnFail(t *testing.T) {
	crypto := new(MockCrypto)
	crypto.On("Encrypt", mock.AnythingOfType("[]uint8"), mock.AnythingOfType("[]uint8")).
		Return(nil, errors.New(genericErrorMessage))

	m, err := NewStatic("bbsPfQTZsmwEcSRKND87WpoC9umuuuOo", crypto)
	if assert.NoError(t, err) {
		key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), staticKMSKeySize)
		if assert.NoError(t, err) {
			_, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
				return m.EncryptKey(context.Background(), keyBytes)
			})
			assert.Error(t, err)
		}
	}
}

func TestStaticKMS_DecryptKey_ReturnsErrorOnFail(t *testing.T) {
	crypto := new(MockCrypto)
	crypto.On("Decrypt", mock.AnythingOfType("[]uint8"), mock.AnythingOfType("[]uint8")).
		Return(nil, errors.New(genericErrorMessage))

	m, err := NewStatic("bbsPfQTZsmwEcSRKND87WpoC9umuuuOo", crypto)
	if assert.NoError(t, err) {
		key, err := internal.GenerateKey(secretFactory, time.Now().Unix(), staticKMSKeySize)
		if assert.NoError(t, err) {
			_, err := internal.WithKeyFunc(key, func(keyBytes []byte) ([]byte, error) {
				return m.DecryptKey(context.Background(), keyBytes)
			})
			assert.Error(t, err)
		}
	}
}

func TestStaticKMS_NewStatic_ReturnsErrorOnInvalidKey(t *testing.T) {
	_, err := NewStatic("bbsPfQTZsmw", aead.NewAES256GCM())
	assert.Error(t, err)
}

func TestStaticKMS_Close(t *testing.T) {
	crypto := aead.NewAES256GCM()
	m, err := NewStatic("bbsPfQTZsmwEcSRKND87WpoC9umuuuOo", crypto)
	require.NoError(t, err)

	assert.False(t, m.key.IsClosed())

	m.Close()

	assert.True(t, m.key.IsClosed())
}
