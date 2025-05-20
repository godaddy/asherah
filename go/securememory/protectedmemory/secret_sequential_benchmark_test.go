package protectedmemory

import (
	"bytes"
	"testing"
)

// BenchmarkProtectedMemorySecret_WithBytes_Sequential runs the benchmark sequentially
// to match Rust's default benchmarking behavior for fair comparison
func BenchmarkProtectedMemorySecret_WithBytes_Sequential(b *testing.B) {
	factory := &SecretFactory{}
	
	orig := []byte("thisismy32bytesecretthatiwilluse")
	copyBytes := make([]byte, len(orig))
	copy(copyBytes, orig)

	s, err := factory.New(orig)
	if err != nil {
		b.Fatal(err)
	}
	defer s.Close()

	b.ResetTimer()
	// Sequential execution - no RunParallel
	for i := 0; i < b.N; i++ {
		err := s.WithBytes(func(gotBytes []byte) error {
			if !bytes.Equal(copyBytes, gotBytes) {
				b.Fatal("bytes don't match")
			}
			return nil
		})
		if err != nil {
			b.Fatal(err)
		}
	}
}