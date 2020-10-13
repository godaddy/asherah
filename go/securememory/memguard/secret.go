// Package memguard implements memguard backed secrets.
package memguard

import (
	"io"
	"sync"
	"time"

	"github.com/awnumar/memguard"
	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/internal/memcall"
	"github.com/godaddy/asherah/go/securememory/internal/secrets"
)

// AllocTimer is used to record the time taken to
// allocate a secret.
var AllocTimer = metrics.GetOrRegisterTimer("secret.memguard.alloctimer", nil)

type secretError string

func (e secretError) Error() string {
	return string(e)
}

const (
	secretCreateErr secretError = "memguard buffer creation failed"
	secretClosedErr secretError = "secret has already been destroyed"
)

// secret contains sensitive memory and stores data in protected page(s) in memory.
// Always call close after use to avoid memory leaks.
type secret struct {
	buffer        *memguard.LockedBuffer
	mc            memcall.Interface
	rw            *sync.RWMutex
	c             *sync.Cond
	closing       bool
	accessCounter int
}

// WithBytes makes the underlying bytes readable and passes them to the function provided.
// A reference MUST not be kept to the bytes passed to the function as the underlying array will no
// longer be readable after the function exits.
func (s *secret) WithBytes(action func([]byte) error) (err error) {
	if err = s.access(); err != nil {
		return
	}

	defer func() {
		if err2 := s.release(); err2 != nil {
			if err == nil {
				err = err2
				return
			}

			err = errors.WithMessage(err, err2.Error())

			return
		}
	}()

	return action(s.buffer.Bytes())
}

// WithBytesFunc makes the underlying bytes readable and passes them to the function provided.
// A reference MUST not be kept to the bytes passed to the function as the underlying array will no
// longer be readable after the function exits.
func (s *secret) WithBytesFunc(action func([]byte) ([]byte, error)) (ret []byte, err error) {
	if err = s.access(); err != nil {
		return
	}

	defer func() {
		if err2 := s.release(); err2 != nil {
			if err == nil {
				err = err2
				return
			}

			err = errors.WithMessage(err, err2.Error())

			return
		}
	}()

	return action(s.buffer.Bytes())
}

// IsClosed returns true if the underlying data container has already been closed
func (s *secret) IsClosed() bool {
	s.rw.RLock()
	defer s.rw.RUnlock()

	return !s.buffer.IsAlive()
}

// Close closes the data container and frees any associated memory.
func (s *secret) Close() error {
	s.rw.Lock()
	defer s.rw.Unlock()

	s.closing = true

	for {
		if !s.buffer.IsAlive() {
			return nil
		}

		if s.accessCounter == 0 {
			// This panics on failure currently
			s.buffer.Destroy()

			securememory.InUseCounter.Dec(1)

			return nil
		}

		s.c.Wait()
	}
}

// access sets the access protection of the data region's memory pages to read-only, if needed.
func (s *secret) access() error {
	s.rw.Lock()
	defer s.rw.Unlock()

	if s.closing || !s.buffer.IsAlive() {
		return errors.WithStack(secretClosedErr)
	}

	// Only set read access if we're the first one trying to access this potentially-shared Secret
	if s.accessCounter == 0 {
		if err := s.mc.Protect(s.buffer.Inner(), memcall.ReadOnly()); err != nil {
			// Shouldn't happen but return the err if it does
			return errors.WithMessage(err, "unable to mark memory as read-only")
		}
	}
	s.accessCounter++

	return nil
}

// release sets the access protection of the data region's memory pages to none, if needed.
func (s *secret) release() error {
	s.rw.Lock()
	defer s.rw.Unlock()
	defer s.c.Broadcast()

	s.accessCounter--
	// Only set no access if we're the last one trying to access this potentially-shared Secret
	if s.accessCounter == 0 {
		if err := s.mc.Protect(s.buffer.Inner(), memcall.NoAccess()); err != nil {
			// Shouldn't happen but return the err if it does
			return errors.WithMessage(err, "unable to mark memory as no-access")
		}
	}

	return nil
}

// NewReader returns a new io.ReadCloser capable of reading from and closing s.
func (s *secret) NewReader() io.Reader {
	return secrets.NewReader(s)
}

// SecretFactory is used to create memguard-based Secret implementations.
type SecretFactory struct {
	mc memcall.Interface
}

func (f *SecretFactory) memcall() memcall.Interface {
	if f.mc == nil {
		f.mc = memcall.Default
	}

	return f.mc
}

// New takes in a byte slice and returns a memguard-backed Secret containing that data.
// The underlying array will be wiped after the function exits.
func (f *SecretFactory) New(b []byte) (securememory.Secret, error) {
	defer AllocTimer.UpdateSince(time.Now())

	lb := memguard.NewBufferFromBytes(b)

	return f.newFromBuffer(lb)
}

func (f *SecretFactory) newFromBuffer(lb *memguard.LockedBuffer) (*secret, error) {
	if !lb.IsAlive() {
		return nil, errors.WithStack(secretCreateErr)
	}

	// Set mprotect to none initially
	if err := f.memcall().Protect(lb.Inner(), memcall.NoAccess()); err != nil {
		// Shouldn't happen, but free up the resources if it does. We intentionally
		// ignore the errors from the cleanup and return the reason why we got here.
		if err2 := memcall.Clean(f.memcall(), lb.Inner()); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		return nil, err
	}

	securememory.AllocCounter.Inc(1)
	securememory.InUseCounter.Inc(1)

	rw := new(sync.RWMutex)

	return &secret{
		rw:     rw,
		c:      sync.NewCond(rw),
		mc:     f.memcall(),
		buffer: lb,
	}, nil
}

// CreateRandom returns a memguard-backed Secret that contains a random byte slice of the specified size.
func (f *SecretFactory) CreateRandom(size int) (securememory.Secret, error) {
	defer AllocTimer.UpdateSince(time.Now())

	lb := memguard.NewBufferRandom(size)

	return f.newFromBuffer(lb)
}
