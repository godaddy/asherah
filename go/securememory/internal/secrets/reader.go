package secrets

import (
	"io"
	"sync"
)

type Reader struct {
	secret BytesWrapper
	mu     sync.Mutex
	i      int
}

// Read implements io.Reader.
func (r *Reader) Read(p []byte) (n int, err error) {
	r.mu.Lock()
	defer r.mu.Unlock()

	err = r.secret.WithBytes(func(b []byte) error {
		if r.i >= len(b) {
			return io.EOF
		}

		n = copy(p, b[r.i:])
		r.i += n

		if r.i >= len(b) {
			return io.EOF
		}

		return nil
	})

	return
}

// NewReader returns a new Reader reading from s.
func NewReader(s BytesWrapper) *Reader {
	return &Reader{
		secret: s,
	}
}
