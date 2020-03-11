package memcall

import "github.com/awnumar/memcall"

type Allocator interface {
	Alloc(size int) ([]byte, error)
}

type Freer interface {
	Free([]byte) error
}

type Protector interface {
	Protect([]byte, MemoryProtectionFlag) error
}

type Locker interface {
	Lock([]byte) error
}

type Unlocker interface {
	Unlock([]byte) error
}

// Interface provides an interface for wrapping memcall functions.
type Interface interface {
	Allocator
	Freer
	Protector
	Locker
	Unlocker
}

// wrapper implements Interface
type wrapper struct {
}

// Default is a default implementation of Interface that directly wraps
// functions exported by the memcall package.
var Default Interface = &wrapper{}

func (*wrapper) Alloc(size int) ([]byte, error) {
	return memcall.Alloc(size)
}

func (*wrapper) Protect(b []byte, mpf MemoryProtectionFlag) error {
	return memcall.Protect(b, mpf)
}

func (*wrapper) Lock(b []byte) error {
	return memcall.Lock(b)
}

func (*wrapper) Unlock(b []byte) error {
	return memcall.Unlock(b)
}

func (*wrapper) Free(b []byte) error {
	return memcall.Free(b)
}
