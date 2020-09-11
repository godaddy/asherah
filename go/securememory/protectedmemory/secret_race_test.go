// +build race_tests

package protectedmemory

import (
	"io/ioutil"
	"runtime"
	"testing"

	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/securememory"
)

func BenchmarkProtectedMemorySecret_WithBytesConcurrentClose(b *testing.B) {
	orig := []byte("thisismy32bytesecretthatiwilluse")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	runRaceTest(b, orig, func(s securememory.Secret) {
		err := s.WithBytes(func(bytes []byte) error {
			assert.Equal(b, copyBytes, bytes)
			return nil
		})
		if err != nil {
			assert.EqualError(b, err, "secret has already been destroyed")
		}
	})
}

func BenchmarkProtectedMemorySecret_ReaderConcurrentClose(b *testing.B) {
	orig := []byte("thisismy32bytesecretthatiwilluse")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	runRaceTest(b, orig, func(s securememory.Secret) {
		r := s.NewReader()

		bytes, err := ioutil.ReadAll(r)
		if err != nil {
			assert.EqualError(b, err, "secret has already been destroyed")
		} else {
			assert.Equal(b, copyBytes, bytes)
		}
	})
}

func runRaceTest(b *testing.B, secretBytes []byte, testFunc func(s securememory.Secret)) {
	s, err := factory.New(secretBytes)
	if assert.NoError(b, err) {
		defer s.Close()

		b.ResetTimer()

		ready := make(chan bool)
		done := make(chan bool)

		go func(ch chan bool) {
			count := 0

			for {
				select {
				case <-ch:
					count++
				case <-done:
					return
				}

				if count >= runtime.GOMAXPROCS(0)/2 {
					assert.NoError(b, s.Close())
				}
			}
		}(ready)

		b.ResetTimer()
		b.RunParallel(func(pb *testing.PB) {
			ready <- true

			for pb.Next() {
				testFunc(s)
			}
		})

		close(done)
		assert.True(b, s.IsClosed())
	}
}
