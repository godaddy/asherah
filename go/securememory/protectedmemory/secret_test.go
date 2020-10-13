// +build !windows

package protectedmemory

import (
	"runtime"
	"sync"
	"testing"
	"time"

	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"
	"golang.org/x/sys/unix"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/internal/memcall"
)

const keySize = 32

var factory = new(SecretFactory)
var errProtect = errors.New("error from protect")

func TestProtectedMemorySecret_Metrics(t *testing.T) {
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

func TestProtectedMemorySecret_WithBytes(t *testing.T) {
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

func TestProtectedMemorySecret_WithBytes_ClosedReturnsError(t *testing.T) {
	m := new(sync.RWMutex)
	s := &secret{
		secretInternal: &secretInternal{
			rw:     m,
			c:      sync.NewCond(m),
			closed: true,
		},
		dummy: nil,
	}

	assert.EqualError(t, s.WithBytes(func(_ []byte) error {
		t.Fail()
		return nil
	}), secretClosedErr.Error())
}

func TestProtectedMemorySecret_WithBytesFunc(t *testing.T) {
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

func TestProtectedMemorySecret_WithBytesFunc_ClosedReturnsError(t *testing.T) {
	m := new(sync.RWMutex)
	s := &secret{
		secretInternal: &secretInternal{
			rw:     m,
			c:      sync.NewCond(m),
			closed: true,
		},
		dummy: nil,
	}

	_, err := s.WithBytesFunc(func(_ []byte) ([]byte, error) {
		t.Fail()
		return nil, nil
	})
	assert.EqualError(t, err, secretClosedErr.Error())
}

func TestProtectedMemorySecret_IsClosed(t *testing.T) {
	orig := []byte("thisismy32bytesecretthatiwilluse")
	sec, err := factory.New(orig)

	if assert.NoError(t, err) {
		assert.False(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
	}
}

func TestProtectedMemorySecret_Close_WithRedundantCall(t *testing.T) {
	orig := []byte("thisismy32bytesecretthatiwilluse")
	sec, err := factory.New(orig)

	if assert.NoError(t, err) {
		assert.False(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
		assert.NoError(t, sec.Close())
		assert.True(t, sec.IsClosed())
	}
}

func TestProtectedMemorySecretFactory_New(t *testing.T) {
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

func TestProtectedMemorySecretFactory_CreateRandom(t *testing.T) {
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

func TestProtectedMemorySecretFactory_CreateRandom_WithError(t *testing.T) {
	secret, e := factory.CreateRandom(-1)
	assert.Nil(t, secret)
	assert.Error(t, e)
}

func TestProtectedMemory_NewSecret(t *testing.T) {
	sec, err := newSecret(keySize, memcall.Default)

	if assert.NoError(t, err) {
		assert.NotNil(t, sec)

		defer sec.Close()

		assert.Equal(t, keySize, len(sec.secretInternal.bytes))
		assert.Equal(t, make([]byte, keySize), sec.secretInternal.bytes)
	}
}

func TestProtectedMemory_NewSecret_InvalidSize(t *testing.T) {
	sec, err := newSecret(-1, memcall.Default)

	if assert.Error(t, err) {
		assert.Nil(t, sec)
	}
}

func TestProtectedMemory_NewSecret_TooLargeToAlloc(t *testing.T) {
	var size int64 = 1 << 62

	sec, err := newSecret(int(size), memcall.Default)

	if assert.Error(t, err) {
		assert.Nil(t, sec)
	}
}

func TestProtectedMemory_NewSecret_MemLockLimit(t *testing.T) {
	origLimit := &unix.Rlimit{}
	err := unix.Getrlimit(unix.RLIMIT_MEMLOCK, origLimit)
	assert.NoError(t, err)

	limit := &unix.Rlimit{
		Cur: 2048,
		Max: origLimit.Max, // lowering hard limit is irreversible
	}
	err = unix.Setrlimit(unix.RLIMIT_MEMLOCK, limit)
	assert.NoError(t, err)

	sec, err := newSecret(keySize, memcall.Default)

	if assert.Error(t, err) {
		assert.Nil(t, sec)
	}

	// Set back to original when done
	err = unix.Setrlimit(unix.RLIMIT_MEMLOCK, origLimit)
	assert.NoError(t, err)
}

func TestProtectedMemory_NewSecret_TriggerFinalizer(t *testing.T) {
	// A lot of this test is based off memguard's finalizer unit test
	sec, err := newSecret(keySize, memcall.Default)

	assert.NoError(t, err)
	assert.NotNil(t, sec)

	secretInternal := sec.secretInternal

	assert.Equal(t, keySize, len(sec.bytes))
	assert.Equal(t, make([]byte, keySize), sec.bytes)
	assert.False(t, sec.IsClosed())

	runtime.KeepAlive(sec)
	// sec now unreachable

	runtime.GC()

	expireAt := time.Now().Add(time.Minute * 5)
	closed := false

	for {
		if secretInternal.isClosed() {
			closed = true
			break
		}

		if time.Now().After(expireAt) {
			break
		}

		runtime.Gosched() // should collect sec
		time.Sleep(time.Millisecond * 5)
	}

	assert.True(t, closed)
}

type MockMemcall struct {
	mock.Mock
}

func (m *MockMemcall) Alloc(size int) ([]byte, error) {
	return make([]byte, size), nil
}

func (m *MockMemcall) Protect(b []byte, mpf memcall.MemoryProtectionFlag) error {
	args := m.Called(b, mpf)
	return args.Error(0)
}

func (m *MockMemcall) Lock(b []byte) error {
	return nil
}

func (m *MockMemcall) Unlock(b []byte) error {
	args := m.Called(b)
	return args.Error(0)
}

func (m *MockMemcall) Free(b []byte) error {
	args := m.Called(b)
	return args.Error(0)
}

func TestProtectedMemorySecretFactory_NewWithMemcallError(t *testing.T) {
	m := new(MockMemcall)

	f := &SecretFactory{
		mc: m,
	}

	data := []byte("testing")

	errUnlock := errors.New("error from unlock")
	errFree := errors.New("error from free")

	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)
	m.On("Unlock", mock.Anything).Return(errUnlock)
	m.On("Free", mock.Anything).Return(errFree)

	secret, err := f.New(data)
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.EqualError(t, err, "error from free: error from unlock: error from protect")

		assert.Nil(t, secret)
	}
}

func TestProtectedMemorySecretFactory_CreateRandomWithMemcallError(t *testing.T) {
	m := new(MockMemcall)

	f := &SecretFactory{
		mc: m,
	}

	size := 8

	errUnlock := errors.New("error from unlock")
	errFree := errors.New("error from free")

	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)
	m.On("Unlock", mock.Anything).Return(errUnlock)
	m.On("Free", mock.Anything).Return(errFree)

	secret, err := f.CreateRandom(size)
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.EqualError(t, err, "error from free: error from unlock: error from protect")

		assert.Nil(t, secret)
	}
}

func TestProtectedMemorySecretFactory_CreateRandomWithRandError(t *testing.T) {
	m := new(MockMemcall)

	f := &SecretFactory{
		mc: m,
	}

	size := 8

	errRandom := errors.New("error from random reader")
	errUnlock := errors.New("error from unlock")
	errFree := errors.New("error from free")

	m.On("Unlock", mock.Anything).Return(errUnlock)
	m.On("Free", mock.Anything).Return(errFree)

	reader := func(b []byte) (int, error) {
		return 0, errRandom
	}

	secret, err := f.createRandom(size, reader)
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errRandom))
		assert.EqualError(t, err, "error from free: error from unlock: error from random reader")

		assert.Nil(t, secret)
	}
}

func TestProtectedMemory_SetReadAccessIfNeeded_MemcallError(t *testing.T) {
	size := 8

	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	sec, err := newSecret(size, m)
	require.NoError(t, err)

	originalAccessCounter := sec.accessCounter

	err = sec.access()
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.Equal(t, originalAccessCounter, sec.accessCounter)
	}
}

func TestProtectedMemory_SetNoAccessIfNeeded_MemcallError(t *testing.T) {
	size := 8

	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	sec, err := newSecret(size, m)
	require.NoError(t, err)

	sec.accessCounter = 1

	err = sec.release()
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
		assert.Equal(t, 0, sec.accessCounter)
	}
}

func TestProtectedMemorySecret_WithBytes_SetReadAccessError(t *testing.T) {
	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	sec, err := newSecret(8, m)
	require.NoError(t, err)

	err = sec.WithBytes(func([]byte) error {
		assert.FailNow(t, "action should not have been called")
		return nil
	})
	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
	}
}

func TestProtectedMemorySecret_WithBytes_SetNoAccessError(t *testing.T) {
	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(nil)
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	sec, err := newSecret(8, m)
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

func TestProtectedMemorySecret_WithBytesFunc_SetReadAccessError(t *testing.T) {
	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(errProtect)

	sec, err := newSecret(8, m)
	require.NoError(t, err)

	_, err = sec.WithBytesFunc(func([]byte) ([]byte, error) {
		assert.FailNow(t, "action should not have been called")
		return nil, nil
	})

	if assert.Error(t, err) {
		assert.True(t, errors.Is(err, errProtect))
	}
}

func TestProtectedMemorySecret_WithBytesFunc_SetNoAccessError(t *testing.T) {
	m := new(MockMemcall)

	m.On("Protect", mock.Anything, memcall.ReadOnly()).Return(nil)
	m.On("Protect", mock.Anything, memcall.NoAccess()).Return(errProtect)

	sec, err := newSecret(8, m)
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
