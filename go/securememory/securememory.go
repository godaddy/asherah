package securememory

import (
	"io"

	"github.com/rcrowley/go-metrics"
)

var (
	// AllocCounter is used to track cumulative secret allocations.
	//
	// AllocCounter increases as secret objects are allocated, but unlike
	// InUseCounter, it does not decrease as secrets are released.
	AllocCounter = metrics.GetOrRegisterCounter("secret.allocated", nil)

	// InUseCounter is used to track the number of secret objects currently in use.
	//
	// InUseCounter increases as secret objects are allocated and decreases
	// as secrets are released.
	InUseCounter = metrics.GetOrRegisterCounter("secret.inuse", nil)
)

// Secret contains sensitive memory and stores data in protected page(s) in memory.
// Always call close after use to avoid memory leaks.
type Secret interface {
	// WithBytes makes the underlying bytes readable and passes them to the function action.
	// It returns the error returned by action.
	//
	// Calling WithBytes on a closed secret is an error.
	//
	// In the event of multiple errors, e.g., action returns a non-nil error and WithBytes encounters
	// an error when modifying the protection state of the underlying byte slice, the errors will be
	// wrapped in a single error and the new composite error is returned.
	//
	// A reference MUST not be kept to the bytes passed to the function as the underlying array will no
	// longer be readable after the function exits.
	WithBytes(action func([]byte) error) error

	// WithBytesFunc makes the underlying bytes readable and passes them to the function action. It
	// returns the byte slice returned by action.
	//
	// See the WithBytes documentation for details on how errors are handled.
	//
	// A reference MUST not be kept to the bytes passed to the function as the underlying array will no
	// longer be readable after the function exits.
	WithBytesFunc(action func([]byte) ([]byte, error)) ([]byte, error)

	// IsClosed returns true if the underlying data container has already been closed.
	IsClosed() bool

	// Close closes the data container and frees any associated memory.
	Close() error

	// NewReader returns a new io.Reader reading from the underlying Secret.
	NewReader() io.Reader
}

// SecretFactory is the interface for creating specific implementations of a Secret.
type SecretFactory interface {
	// New takes in a byte slice and returns a Secret containing that data.
	New(b []byte) (Secret, error)

	// CreateRandom returns a Secret that contains a random byte slice of the specified size.
	CreateRandom(size int) (Secret, error)
}
