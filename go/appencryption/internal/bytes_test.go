package internal

import (
	"testing"

	"github.com/pkg/errors"
	"github.com/stretchr/testify/assert"
)

func TestMemClr(t *testing.T) {
	orig := []byte("testing")

	MemClr(orig)

	allZero := true

	for i := range orig {
		if orig[i] != 0 {
			allZero = false
		}
	}

	assert.True(t, allZero)
}

type ErrorReader struct {
}

func (ErrorReader) Read(p []byte) (n int, err error) {
	return 0, errors.New("error reading from stream")
}

func Test_FillRandom(t *testing.T) {
	size := 30
	b := make([]byte, size)
	assert.Equal(t, make([]byte, size), b)

	FillRandom(b)

	assert.Len(t, b, size)
	assert.NotEqual(t, make([]byte, size), b)
}

func Test_FillRandom_Panics(t *testing.T) {
	r := ErrorReader{}

	assert.Panics(t, func() {
		fillRandom(make([]byte, 12), r.Read)
	})
}

func Test_GetRandBytes(t *testing.T) {
	size := 20
	b := GetRandBytes(size)

	assert.Len(t, b, size)
	assert.NotEqual(t, make([]byte, size), b)
}
