package internal

import (
	"crypto/rand"
	"runtime"
)

// MemClr takes a buffer and wipes it with zeroes.
func MemClr(buf []byte) {
	for i := range buf {
		buf[i] = 0
	}

	// Prevent dead store elimination, based on https://github.com/golang/go/issues/33325
	// and https://github.com/awnumar/memguard/blob/fb1272668ab3188606f9dfec73b2f7865a30603d/core/crypto.go#L105.
	// Avoid using memguard directly here in case we change our default secure memory implementation.
	runtime.KeepAlive(buf)
}

// FillRandom takes a buffer and overwrites it with cryptographically-secure random bytes.
func FillRandom(buf []byte) {
	fillRandom(buf, rand.Read)
}

func fillRandom(buf []byte, r func([]byte) (int, error)) {
	if _, err := r(buf); err != nil {
		panic(err)
	}

	// Prevent dead store elimination in case a caller wants the backing array randomized even if no longer used.
	// Copied from memguard implementation that references https://github.com/golang/go/issues/33325
	runtime.KeepAlive(buf)
}

// GetRandBytes returns a slice of a specified length, filled with cryptographically-secure random bytes.
func GetRandBytes(n int) []byte {
	buf := make([]byte, n)
	FillRandom(buf)

	return buf
}
