//go:build nobenchlock
// +build nobenchlock

package protectedmemory

import (
	"log"
)

// disableLocking is a compile-time flag to disable memory locking for benchmarking.
// WARNING: This must NEVER be used in production as it defeats the security
// purpose of protected memory. Only use with the nobenchlock build tag.
func init() {
	log.Println("WARNING: Memory locking disabled for benchmarking - DO NOT USE IN PRODUCTION")
}

// Override the lock function when nobenchlock is specified
func lockMemory(b []byte) error {
	// No-op for benchmarking
	return nil
}

// Override the unlock function when nobenchlock is specified
func unlockMemory(b []byte) error {
	// No-op for benchmarking
	return nil
}