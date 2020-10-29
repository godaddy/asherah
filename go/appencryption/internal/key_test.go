package internal

import (
	"fmt"
	"io"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"
)

const keySize = 32

var (
	secretFactory = new(memguard.SecretFactory)
	created       = time.Now().Unix()
)

type MockSecret struct {
	mock.Mock
}

func (m *MockSecret) WithBytes(action func([]byte) error) error {
	ret := m.Called(action)

	if err := ret.Error(0); err != nil {
		return err
	}

	return nil
}

func (m *MockSecret) WithBytesFunc(action func([]byte) ([]byte, error)) ([]byte, error) {
	ret := m.Called(action)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (m *MockSecret) IsClosed() bool {
	ret := m.Called()

	var closed bool
	if b := ret.Get(0); b != nil {
		closed = b.(bool)
	}

	return closed
}

func (m *MockSecret) Close() error {
	ret := m.Called()

	if err := ret.Error(0); err != nil {
		return err
	}

	return nil
}

func (m *MockSecret) NewReader() io.Reader {
	ret := m.Called()

	return ret.Get(0).(io.Reader)
}

func TestCryptoKey_Getters(t *testing.T) {
	key := &CryptoKey{
		created: created,
		revoked: 1,
	}

	assert.Equal(t, created, key.Created())
	assert.True(t, key.Revoked())
}

func TestCryptoKey_SetRevoked_WithTrue(t *testing.T) {
	key := &CryptoKey{
		created: time.Now().Unix(),
		revoked: 0,
	}
	assert.False(t, key.Revoked())

	key.SetRevoked(true)

	assert.True(t, key.Revoked())
}

func TestCryptoKey_SetRevoked_WithFalse(t *testing.T) {
	key := &CryptoKey{
		created: time.Now().Unix(),
		revoked: 1,
	}
	assert.True(t, key.Revoked())

	key.SetRevoked(false)

	assert.False(t, key.Revoked())
}

func TestCryptoKey_Close(t *testing.T) {
	sec, err := secretFactory.New([]byte("testing"))

	if assert.NoError(t, err) {
		key := &CryptoKey{
			secret: sec,
		}

		assert.False(t, key.IsClosed())
		key.Close()
		assert.True(t, key.IsClosed())
		assert.NotPanics(t, func() {
			key.Close()
		})
	}
}

func TestCryptoKey_String(t *testing.T) {
	sec, err := secretFactory.New([]byte("testing"))
	require.NoError(t, err)

	key := &CryptoKey{secret: sec}
	defer key.Close()

	expected := fmt.Sprintf("CryptoKey(%p){secret(%p)}", key, sec)
	assert.Equal(t, expected, key.String())
}

func TestNewCryptoKey(t *testing.T) {
	bytes := []byte("blah")
	bytesCopy := make([]byte, len(bytes))
	copy(bytesCopy, bytes)

	key, err := NewCryptoKey(secretFactory, created, false, bytes)
	if assert.NoError(t, err) {
		assert.Equal(t, created, key.created)
		assert.Equal(t, uint32(0), key.revoked)
		assert.NoError(t, WithKey(key, func(keyBytes []byte) error {
			assert.Equal(t, bytesCopy, keyBytes)
			return nil
		}))
		assert.Equal(t, make([]byte, len(bytes)), bytes)
	}
}

func TestNewCryptoKey_WithRevokedTrue(t *testing.T) {
	bytes := []byte("blah")
	bytesCopy := make([]byte, len(bytes))
	copy(bytesCopy, bytes)

	key, err := NewCryptoKey(secretFactory, created, true, bytes)
	if assert.NoError(t, err) {
		assert.Equal(t, created, key.created)
		assert.Equal(t, uint32(1), key.revoked)
		assert.NoError(t, WithKey(key, func(keyBytes []byte) error {
			assert.Equal(t, bytesCopy, keyBytes)
			return nil
		}))
		assert.Equal(t, make([]byte, len(bytes)), bytes)
	}
}

func TestGenerateKey(t *testing.T) {
	key, err := GenerateKey(secretFactory, created, keySize)
	if err != nil {
		t.Error("expected nil error; got", err)
	}

	defer key.Close()

	if assert.NoError(t, err) {
		assert.NotNil(t, key)
		assert.Equal(t, created, key.created)
		assert.NoError(t, WithKey(key, func(bytes []byte) error {
			assert.Len(t, bytes, keySize)
			return nil
		}))
	}
}

func TestWithKey(t *testing.T) {
	mockSecret := new(MockSecret)
	key := &CryptoKey{
		secret: mockSecret,
	}
	magicErr := errors.New("magic err returned to signal success")
	mockSecret.On("WithBytes", mock.Anything).Return(magicErr)

	err := WithKey(key, func(bytes []byte) error {
		return nil
	})
	if assert.Error(t, err) {
		assert.EqualError(t, err, magicErr.Error())
	}
}

func TestWithKeyFunc(t *testing.T) {
	mockSecret := new(MockSecret)
	key := &CryptoKey{
		secret: mockSecret,
	}
	bytes := []byte("success")
	mockSecret.On("WithBytesFunc", mock.Anything).Return(bytes, nil)

	ret, err := WithKeyFunc(key, func(bytes []byte) ([]byte, error) {
		return nil, nil
	})
	if assert.NoError(t, err) {
		assert.Equal(t, bytes, ret)
	}
}

func BenchmarkGenerateKey(b *testing.B) {
	var key *CryptoKey

	for i := 0; i < b.N; i++ {
		key, _ = GenerateKey(secretFactory, time.Now().Unix(), keySize)
		key.Close()
	}

	b.ReportAllocs()
}
