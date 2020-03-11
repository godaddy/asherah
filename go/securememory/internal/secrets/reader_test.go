package secrets_test

import (
	"fmt"
	"io"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/memguard"
	"github.com/godaddy/asherah/go/securememory/protectedmemory"
)

var factories = map[string]securememory.SecretFactory{
	"memguard":        new(memguard.SecretFactory),
	"protectedmemory": new(protectedmemory.SecretFactory),
}

func TestReader(t *testing.T) {
	tests := []struct {
		n        int
		expected string
		readerr  error
	}{
		{n: 4, expected: "0123"},
		{n: 4, expected: "4567"},
		{n: 1, expected: "8"},
		{n: 4, expected: "9", readerr: io.EOF},
		{n: 4, expected: "", readerr: io.EOF},
	}

	for name, factory := range factories {
		orig := []byte("0123456789")

		s, err := factory.New(orig)
		require.NoError(t, err)

		r := s.NewReader()

		for i, tt := range tests {
			tt := tt

			t.Run(fmt.Sprintf("%s-%d", name, i), func(t *testing.T) {
				buf := make([]byte, tt.n)
				n, err := r.Read(buf)
				assert.Equal(t, tt.readerr, err)
				assert.True(t, n <= tt.n)
				assert.Equal(t, tt.expected, string(buf[:n]))
			})
		}

		s.Close()
	}
}

func TestReaderReadAfterClose(t *testing.T) {
	for name := range factories {
		factory := factories[name]

		t.Run(name, func(t *testing.T) {
			orig := []byte("testing")

			s, err := factory.New(orig)
			require.NoError(t, err)

			r := s.NewReader()
			require.NoError(t, s.Close())

			buf := make([]byte, len(orig))

			n, err := r.Read(buf)
			if assert.EqualError(t, err, "secret has already been destroyed") {
				assert.Equal(t, 0, n)
			}
		})
	}
}
