//go:build nobenchlock && darwin
// +build nobenchlock,darwin

package memcall

import (
	"runtime"
)

// Darwin-specific no-op benchmarking implementation for memory operations
// This file is only used when building with the nobenchlock tag on Darwin systems

// Lock is a no-op implementation for benchmarking on Darwin
func (m memcallDarwin) Lock(b []byte) error {
	if runtime.GOOS != "darwin" {
		panic("nobenchlock feature is only supported on Darwin")
	}
	// No-op for Darwin benchmarking mode
	return nil
}

// Unlock is a no-op implementation for benchmarking on Darwin
func (m memcallDarwin) Unlock(b []byte) error {
	if runtime.GOOS != "darwin" {
		panic("nobenchlock feature is only supported on Darwin")
	}
	// No-op for Darwin benchmarking mode
	return nil
}