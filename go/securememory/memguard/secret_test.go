package memguard

import (
	"sync"
	"testing"

	"github.com/awnumar/memguard"
	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/internal/memcall"
)

const keySize = 32

var factory = new(SecretFactory)
var errProtect = errors.New("error from protect")

func TestMemguardSecret_Metrics(t *testing.T) {
	// reset the counters
	securememory.AllocCounter.Clear()
	securememory.InUseCounter.Clear()

	assert.Equal(t, int64(0), securememory.AllocCounter.Count())
	assert.Equal(t, int64(0), securememory.InUseCounter.Count())

	// count is the number of secrets per factory constructor (New and CreateRandom)
	const count int64 = 10

	func() {
		for i := int64(0); i < count; i++ {
			orig := []byte("testing")
			copyBytes := make([]byte, len(orig))
			copy(copyBytes, orig)

			s, err := factory.New(orig)
			require.NoError(t, err)

			defer s.Close()

			require.NoError(t, s.WithBytes(func(b []byte) error {
				assert.Equal(t, copyBytes, b)
				return nil
			}))

			r, err := factory.CreateRandom(8)
			require.NoError(t, err)

			defer r.Close()

			require.NoError(t, r.WithBytes(func(b []byte) error {
				assert.Equal(t, 8, len(b))
				return nil
			}))
		}

		assert.Equal(t, count*2, securememory.AllocCounter.Count())
		assert.Equal(t, count*2, securememory.InUseCounter.Count())
	}()

	assert.Equal(t, count*2, securememory.AllocCounter.Count())
	assert.Equal(t, int64(0), securememory.InUseCounter.Count())
}

func TestMemguardSecret_WithBytes(t *testing.T) {
	orig := []byte("testing")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	s, err := factory.New(orig)
	if assert.NoError(t, err) {
		defer s.Close()
		assert.NoError(t, s.WithBytes(func(b []byte) error {
			assert.Equal(t, copyBytes, b)
			return nil
		}))
	}
}

func TestMemguardSecret_WithBytes_DestroyedReturnsError(t *testing.T) {
	b := memguard.NewBufferRandom(keySize)
	if assert.True(t, b.IsAlive()) {
		m := new(sync.RWMutex)
		s := &secret{
			rw:     m,
			c:      sync.NewCond(m),
			buffer: b,
		}

		b.Destroy()
		assert.EqualError(t, s.WithBytes(func(_ []byte) error {
			t.Fail()
			return nil
		}), secretClosedErr.Error())
	}
}

func TestMemguardSecret_WithBytesFunc(t *testing.T) {
	orig := []byte("testing")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	s, err := factory.New(orig)
	if assert.NoError(t, err) {
		defer s.Close()
		_, err := s.WithBytesFunc(func(b []byte) ([]byte, error) {
			assert.Equal(t, copyBytes, b)
			return b, nil
		})
		assert.NoError(t, err)
	}
}

func TestMemguardSecret_WithBytesFunc_DestroyedReturnsError(t *testing.T) {
	b := memguard.NewBufferRandom(keySize)
	if assert.True(t, b.IsAlive()) {
		m := new(sync.RWMutex)
		s := &secret{
			rw:     m,
			c:      sync.NewCond(m),
			buffer: b,
		}

		b.Destroy()

		_, err := s.WithBytesFunc(func(_ []byte) ([]byte, error) {
			t.Fail()
			return nil, nil
		})
		assert.EqualError(t, err, secretClosedErr.Error())
	}
}

func TestMemguardSecret_IsClosed(t *testing.T) {
	sec, err := factory.New([]byte("testing"))
	if assert.NoError(t, err) {
		assert.False(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
	}
}

func TestMemguardSecret_Close_WithRedundantCall(t *testing.T) {
	sec, err := factory.New([]byte("testing"))
	if assert.NoError(t, err) {
		assert.False(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
	}
}

func TestMemguardSecretFactory_New(t *testing.T) {
	orig := []byte("testing")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	tests := []struct {
		Name   string
		Error  bool
		Buffer []byte
	}{
		{
			Name:   "returns error",
			Buffer: nil,
			Error:  true,
		},
		{
			Name:   "returns no error",
			Buffer: orig,
			Error:  false,
		},
	}

	for _, tt := range tests {
		tt := tt
		t.Run(tt.Name, func(t *testing.T) {
			b, err := factory.New(tt.Buffer)
			if tt.Error && assert.Error(t, err) {
				assert.Nil(t, b)
			} else if assert.NoError(t, err) {
				assert.NotNil(t, b)
				assert.NoError(t, b.WithBytes(func(bytes []byte) error {
					assert.Equal(t, len(copyBytes), len(bytes))
					assert.Equal(t, copyBytes, bytes)
					return nil
				}))
				defer b.Close()
			}
		})
	}
}

func TestMemguardSecretFactory_CreateRandom(t *testing.T) {
	size := 8

	assert.NotPanics(t, func() {
		secret, err := factory.CreateRandom(size)
		if assert.NoError(t, err) {
			assert.NoError(t, secret.WithBytes(func(bytes []byte) error {
				assert.Equal(t, size, len(bytes))
				return nil
			}))
			defer secret.Close()
		}
	})
}

func TestMemguardSecretFactory_CreateRandom_WithError(t *testing.T) {
	secret, e := factory.CreateRandom(-1)
	assert.Nil(t, secret)
	assert.EqualError(t, e, secretCreateErr.Error())
}

type MockMemcall struct {
	mock.Mock
}

func (m *MockMemcall) Alloc(size int) ([]byte, error) {
	return make([]byte, size), nil
}

func (m *MockMemcall) Protect(b []byte, mpf memcall.MemoryProtectionFlag) error {
	// b is owned by memguard, so we MUST not access here to prevent segfault
	args := m.Called(mock.Anything, mpf)
	return args.Error(0)
}

func (m *MockMemcall) Lock(b []byte) error {
	// b is owned by memguard, so we MUST not access here to prevent segfault
	return nil
}

func (m *MockMemcall) Unlock(b []byte) error {
	// b is owned by memguard, so we MUST not access here to prevent segfault
	args := m.Called(mock.Anything)
	return args.Error(0)
}

func (m *MockMemcall) Free(b []byte) error {
	// b is owned by memguard, so we MUST not access here to prevent segfault
	args := m.Called(mock.Anything)
	return args.Error(0)
}

func TestMemguardSecretFactory_NewWithMemcallError(t *testing.T) {
	m := new(MockMemcall)

	f := &SecretFactory{
		mc: m,
	}

	data := []byte("testing")

	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)
	m.On("Unlock", mock.Anything).Return(errors.New("error from unlock"))
	m.On("Free", mock.Anything).Return(errors.New("error from free"))

	secret, err := f.New(data)
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.EqualError(t, err, "error from free: error from unlock: error from protect")

		assert.Nil(t, secret)
	}
}

func TestMemguardSecretFactory_CreateRandomWithMemcallError(t *testing.T) {
	m := new(MockMemcall)

	f := &SecretFactory{
		mc: m,
	}

	size := 8

	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)
	m.On("Unlock", mock.Anything).Return(errors.New("error from unlock"))
	m.On("Free", mock.Anything).Return(errors.New("error from free"))

	secret, err := f.CreateRandom(size)
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.EqualError(t, err, "error from free: error from unlock: error from protect")

		assert.Nil(t, secret)
	}
}

func TestMemguardSecret_SetReadAccessIfNeeded_MemcallError(t *testing.T) {
	size := 8

	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil)
	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	s, err := f.CreateRandom(size)
	require.NoError(t, err)

	sec := s.(*secret)
	originalAccessCounter := sec.accessCounter

	err = sec.access()
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.Equal(t, originalAccessCounter, sec.accessCounter)
	}
}

func TestMemguardSecret_SetNoAccessIfNeeded_MemcallError(t *testing.T) {
	size := 8

	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil).Once()
	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	s, err := f.CreateRandom(size)
	require.NoError(t, err)

	sec := s.(*secret)
	sec.accessCounter = 1

	err = sec.release()
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.Equal(t, 0, sec.accessCounter)
	}
}

func TestMemguardSecret_WithBytes_SetReadAccessError(t *testing.T) {
	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil)
	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	sec, err := f.CreateRandom(8)
	require.NoError(t, err)

	err = sec.WithBytes(func([]byte) error {
		assert.FailNow(t, "action should not have been called")
		return nil
	})

	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
	}
}

func TestMemguardSecret_WithBytes_SetNoAccessError(t *testing.T) {
	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil).Once()

	// this one is from setReadAccessIfNeeded
	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(nil)

	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	sec, err := f.CreateRandom(8)
	require.NoError(t, err)

	called := false
	err = sec.WithBytes(func([]byte) error {
		called = true

		return nil
	})

	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect), "expected protect error")
		assert.True(t, called, "WithBytes action func not called")
	}

	called = false
	err = sec.WithBytes(func([]byte) error {
		called = true

		return errors.New("action failed")
	})

	if assert.Error(t, err) {
		assert.True(t, called, "WithBytes action func not called")
		assert.EqualError(t, err, "unable to mark memory as no-access: error from protect: action failed")
	}
}

func TestMemguardSecret_WithBytesFunc_SetReadAccessError(t *testing.T) {
	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil)
	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	sec, err := f.CreateRandom(8)
	require.NoError(t, err)

	_, err = sec.WithBytesFunc(func([]byte) ([]byte, error) {
		assert.FailNow(t, "action should not have been called")
		return nil, nil
	})

	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
	}
}

func TestMemguardSecret_WithBytesFunc_SetNoAccessError(t *testing.T) {
	m := new(MockMemcall)

	// first called during CreateRandom
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(nil).Once()

	// this one is from setReadAccessIfNeeded
	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(nil)

	// we need the second call to trigger an error
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	f := &SecretFactory{
		mc: m,
	}

	sec, err := f.CreateRandom(8)
	require.NoError(t, err)

	called := false
	_, err = sec.WithBytesFunc(func([]byte) ([]byte, error) {
		called = true

		return nil, nil
	})

	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect), "expected protect error")
		assert.True(t, called, "WithBytes action func not called")
	}

	called = false
	_, err = sec.WithBytesFunc(func([]byte) ([]byte, error) {
		called = true

		return nil, errors.New("action failed")
	})

	if assert.Error(t, err) {
		assert.True(t, called, "WithBytes action func not called")
		assert.EqualError(t, err, "unable to mark memory as no-access: error from protect: action failed")
	}
}
