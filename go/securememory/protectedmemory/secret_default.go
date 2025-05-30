//go:build !nobenchlock
// +build !nobenchlock

package protectedmemory

import "github.com/godaddy/asherah/go/securememory/internal/memcall"

// lockMemory locks memory pages to prevent swapping to disk
// This is the default implementation used in production
func lockMemory(b []byte) error {
	return memcall.Default.Lock(b)
}

// unlockMemory unlocks memory pages
// This is the default implementation used in production
func unlockMemory(b []byte) error {
	return memcall.Default.Unlock(b)
}