package internal_test

import (
	"testing"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache/internal"
)

func TestCountMinSketch(t *testing.T) {
	const max = 15

	cm := &internal.CountMinSketch{}
	cm.Init(max)

	for i := 0; i < max; i++ {
		// Increase value at i j times
		for j := i; j > 0; j-- {
			cm.Add(uint64(i))
		}
	}

	for i := 0; i < max; i++ {
		n := cm.Estimate(uint64(i))
		if int(n) != i {
			t.Fatalf("unexpected estimate(%d): %d, want: %d", i, n, i)
		}
	}

	cm.Reset()

	for i := 0; i < max; i++ {
		n := cm.Estimate(uint64(i))
		if int(n) != i/2 {
			t.Fatalf("unexpected estimate(%d): %d, want: %d", i, n, i/2)
		}
	}

	cm.Reset()

	for i := 0; i < max; i++ {
		n := cm.Estimate(uint64(i))
		if int(n) != i/4 {
			t.Fatalf("unexpected estimate(%d): %d, want: %d", i, n, i/4)
		}
	}

	for i := 0; i < 100; i++ {
		cm.Add(1)
	}

	if n := cm.Estimate(1); n != 15 {
		t.Fatalf("unexpected estimate(%d): %d, want: %d", 1, n, 15)
	}
}

func BenchmarkCountMinSketchReset(b *testing.B) {
	cm := &internal.CountMinSketch{}
	cm.Init(1<<15 - 1)

	b.ResetTimer()
	b.ReportAllocs()

	for i := 0; i < b.N; i++ {
		cm.Add(0xCAFECAFECAFECAFE)
		cm.Reset()
	}
}
