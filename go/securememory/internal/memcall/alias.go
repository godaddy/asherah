package memcall

import "github.com/awnumar/memcall"

// MemoryProtectionFlag specifies some particular memory protection flag.
type MemoryProtectionFlag = memcall.MemoryProtectionFlag

// NoAccess specifies that the memory should be marked unreadable and immutable.
func NoAccess() MemoryProtectionFlag {
	return memcall.NoAccess()
}

// ReadOnly specifies that the memory should be marked read-only (immutable).
func ReadOnly() MemoryProtectionFlag {
	return memcall.ReadOnly()
}

// ReadWrite specifies that the memory should be made readable and writable.
func ReadWrite() MemoryProtectionFlag {
	return memcall.ReadWrite()
}
