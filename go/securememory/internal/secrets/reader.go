package secrets

import "io"

type Reader struct {
	secret BytesWrapper
	i      int
}

// Read implements io.Reader
func (r *Reader) Read(p []byte) (n int, err error) {
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
