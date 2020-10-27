// Package protectedmemory implements protected memory backed secrets.
package protectedmemory

import (
	"crypto/rand"
	"crypto/subtle"
	"fmt"
	"io"
	"runtime"
	"runtime/debug"
	"sync"
	"time"

	// NOTE: If we ever remove the import of core, we'll need to add an init func that calls memcall.DisableCoreDumps
	"github.com/awnumar/memguard/core"
	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/internal/memcall"
	"github.com/godaddy/asherah/go/securememory/internal/secrets"
	"github.com/godaddy/asherah/go/securememory/log"
)

// AllocTimer is used to record the time taken to allocate a secret.
var AllocTimer = metrics.GetOrRegisterTimer("secret.protectedmemory.alloctimer", nil)

type secretError string

func (e secretError) Error() string {
	return string(e)
}

const secretClosedErr secretError = "secret has already been destroyed"

// secret contains sensitive memory and stores data in protected page(s) in memory.
// Always call close after use to avoid memory leaks.
type secret struct {
	*secretInternal
	// dummy is used for attaching a finalizer since attaching one to the secret itself results in it always having a reference.
	dummy *bool
}

// secretInternal is an abstraction needed to allow us to close the secret without referencing it directly in a finalizer.
type secretInternal struct {
	bytes   []byte
	mc      memcall.Interface
	rw      *sync.RWMutex
	c       *sync.Cond
	closing bool
	closed  bool

	// stack contains a formatted stack trace collected when the secret was created, only set if DebugEnabled.
	stack        []byte
	externalAddr string

	//nolint:structcheck // False positive of being unused, see https://github.com/golangci/golangci-lint/issues/572
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

	return action(s.bytes)
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

	return action(s.bytes)
}

// IsClosed returns true if the underlying data container has already been closed
func (s *secret) IsClosed() bool {
	return s.isClosed()
}

// NewReader returns a new io.Reader capable of reading from s.
func (s *secret) NewReader() io.Reader {
	return secrets.NewReader(s)
}

// access sets the access protection of the data region's memory pages to read-only, if needed.
func (s *secretInternal) access() (err error) {
	s.rw.Lock()
	defer s.rw.Unlock()

	if s.closing || s.closed {
		return errors.WithStack(secretClosedErr)
	}

	// Only set read access if we're the first one trying to access this potentially-shared Secret
	if s.accessCounter == 0 {
		if err := s.mc.Protect(s.bytes, memcall.ReadOnly()); err != nil {
			// Shouldn't happen but return the err if it does
			return errors.WithMessage(err, "unable to mark memory as read-only")
		}
	}
	s.accessCounter++

	return nil
}

// release sets the access protection of the data region's memory pages to none, if needed.
func (s *secretInternal) release() error {
	s.rw.Lock()
	defer s.rw.Unlock()
	defer s.c.Broadcast()

	s.accessCounter--
	// Only set no access if we're the last one trying to access this potentially-shared secret
	if s.accessCounter == 0 {
		if err := s.mc.Protect(s.bytes, memcall.NoAccess()); err != nil {
			// Shouldn't happen but return the err if it does
			return errors.WithMessage(err, "unable to mark memory as no-access")
		}
	}

	return nil
}

// isClosed is the actual implementation of secret.IsClosed. It needs to be implemented at this level in order
// to unit test the finalizer (to avoid a reference to the secret).
func (s *secretInternal) isClosed() bool {
	s.rw.RLock()
	defer s.rw.RUnlock()

	return s.closed
}

func (s *secretInternal) Finalize() {
	s.rw.Lock()
	if !s.closing {
		log.Debugf("finalized before closed: secret(%s){inner(%p)}\n%s\n", s.externalAddr, s, s.stack)
	}
	s.rw.Unlock()

	s.Close()
}

// Close closes the data container and frees any associated memory.
func (s *secretInternal) Close() error {
	s.rw.Lock()
	defer s.rw.Unlock()

	s.closing = true

	for {
		if s.closed {
			return nil
		}

		if s.accessCounter == 0 {
			return s.close()
		}

		s.c.Wait()
	}
}

// close is the actual implementation of secret.Close. It needs to be implemented at this level in order for
// the finalizer to work properly (to avoid a reference to the secret).
func (s *secretInternal) close() (err error) {
	if err := s.mc.Protect(s.bytes, memcall.ReadWrite()); err != nil {
		return err
	}

	// Wipe the memory.
	core.Wipe(s.bytes)

	// Unlock pages locked into memory.
	if err := s.mc.Unlock(s.bytes); err != nil {
		return err
	}

	// Free all related memory.
	if err := s.mc.Free(s.bytes); err != nil {
		return err
	}

	s.bytes = nil
	s.closed = true

	securememory.InUseCounter.Dec(1)

	return nil
}

// SecretFactory is used to create protected memory based Secret implementations.
type SecretFactory struct {
	mc memcall.Interface
}

func (f *SecretFactory) memcall() memcall.Interface {
	if f.mc == nil {
		f.mc = memcall.Default
	}

	return f.mc
}

// New takes in a byte slice and returns a protected memory backed Secret containing that data.
// The underlying array will be wiped after the function exits.
func (f *SecretFactory) New(b []byte) (securememory.Secret, error) {
	defer AllocTimer.UpdateSince(time.Now())

	secret, err := newSecret(len(b), f.memcall())
	if err != nil {
		return nil, err
	}

	// copy b into bytes and wipe the source
	subtle.ConstantTimeCopy(1, secret.bytes, b)
	core.Wipe(b)

	// Set mprotect to none initially
	if err := f.memcall().Protect(secret.bytes, memcall.NoAccess()); err != nil {
		// Shouldn't happen, but free up the resources if it does. We intentionally
		// ignore the errors from the cleanup and return the reason why we got here.
		if err2 := memcall.Clean(f.memcall(), secret.bytes); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		return nil, err
	}

	securememory.AllocCounter.Inc(1)
	securememory.InUseCounter.Inc(1)

	return secret, nil
}

// CreateRandom returns a protected memory backed Secret that contains a random byte slice of the specified size.
func (f *SecretFactory) CreateRandom(size int) (securememory.Secret, error) {
	return f.createRandom(size, rand.Read)
}

func (f *SecretFactory) createRandom(size int, readFunc func(b []byte) (n int, err error)) (securememory.Secret, error) {
	defer AllocTimer.UpdateSince(time.Now())

	s, err := newSecret(size, f.memcall())
	if err != nil {
		return nil, err
	}

	// copy b into bytes and wipe the source
	if _, err := readFunc(s.bytes); err != nil {
		// Shouldn't happen, but free up the resources if it does. We intentionally
		// ignore the errors from the cleanup and return the reason why we got here.
		if err2 := memcall.Clean(f.memcall(), s.bytes); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		return nil, err
	}

	// Set mprotect to none initially
	if err := f.memcall().Protect(s.bytes, memcall.NoAccess()); err != nil {
		// Shouldn't happen, but free up the resources if it does. We intentionally
		// ignore the errors from the cleanup and return the reason why we got here.
		if err2 := f.memcall().Unlock(s.bytes); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		if err2 := f.memcall().Free(s.bytes); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		return nil, err
	}

	securememory.AllocCounter.Inc(1)
	securememory.InUseCounter.Inc(1)

	return s, nil
}

// newSecret handles the core allocation/setup of a new secret of the given size.
func newSecret(size int, mc memcall.Interface) (*secret, error) {
	if size < 1 {
		return nil, errors.New("invalid secret length")
	}

	// allocate memory via mmap (will round up to next page size)
	bytes, err := mc.Alloc(size)
	if err != nil {
		return nil, err
	}

	// lock memory via mlock (don't page to disk)
	if err := mc.Lock(bytes); err != nil {
		// if mlock fails, try to deallocate/munmap the memory. We intentionally ignore the errors from
		// the cleanup and return the reason why we got here.
		if err2 := mc.Free(bytes); err2 != nil {
			err = errors.Wrap(err, err2.Error())
		}

		return nil, err
	}

	// We have to use a wrapper structure with a dummy reference for the finalizer to trigger properly
	rw := new(sync.RWMutex)
	internal := &secretInternal{
		rw:    rw,
		c:     sync.NewCond(rw),
		mc:    mc,
		bytes: bytes,
	}

	secret := &secret{
		secretInternal: internal,
		dummy:          new(bool),
	}

	if log.DebugEnabled() {
		internal.externalAddr = fmt.Sprintf("%p", secret)
		internal.stack = debug.Stack()
	}

	// Finalizer attaches to dummy reference so we can cleanup secret when it goes out of scope. We have to use
	// secretInternal to call close to avoid keeping the secret in scope by virtue of the finalizer setup.
	runtime.SetFinalizer(secret.dummy, func(_ *bool) {
		go internal.Finalize()
	})

	return secret, nil
}
