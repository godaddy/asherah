package internal

import (
	"math"
)

// BloomFilter is Bloom Filter implementation used as a cache admission policy.
// See http://billmill.org/bloomfilter-tutorial/
type BloomFilter struct {
	numHashes uint32   // number of hashes per element
	bitsMask  uint32   // size of bit vector
	bits      []uint64 // filter bit vector
}

// Init initializes bloomFilter with the given expected insertions ins and
// false positive probability fpp.
func (f *BloomFilter) Init(ins int, fpp float64) {
	ln2 := math.Log(2.0)
	factor := -math.Log(fpp) / (ln2 * ln2)

	numBits := nextPowerOfTwo(uint32(float64(ins) * factor))
	if numBits == 0 {
		numBits = 1
	}

	f.bitsMask = numBits - 1

	if ins == 0 {
		f.numHashes = 1
	} else {
		f.numHashes = uint32(ln2 * float64(numBits) / float64(ins))
	}

	if size := int(numBits+63) / 64; len(f.bits) != size {
		f.bits = make([]uint64, size)
	} else {
		f.Reset()
	}
}

// nextPowerOfTwo returns the smallest power of two which is greater than or equal to i.
func nextPowerOfTwo(i uint32) uint32 {
	n := i - 1
	n |= n >> 1
	n |= n >> 2
	n |= n >> 4
	n |= n >> 8
	n |= n >> 16
	n++

	return n
}

// Put inserts a hash value into the bloom filter.
// It returns true if the value may already in the filter.
func (f *BloomFilter) Put(h uint64) bool {
	h1, h2 := uint32(h), uint32(h>>32)

	var o uint = 1
	for i := uint32(0); i < f.numHashes; i++ {
		o &= f.set((h1 + (i * h2)) & f.bitsMask)
	}

	return o == 1
}

// contains returns true if the given hash is may be in the filter.
func (f *BloomFilter) Contains(h uint64) bool {
	h1, h2 := uint32(h), uint32(h>>32)

	var o uint = 1
	for i := uint32(0); i < f.numHashes; i++ {
		o &= f.get((h1 + (i * h2)) & f.bitsMask)
	}

	return o == 1
}

// set sets bit at index i and returns previous value.
func (f *BloomFilter) set(i uint32) uint {
	idx, shift := i/64, i%64
	val := f.bits[idx]
	mask := uint64(1) << shift
	f.bits[idx] |= mask

	return uint((val & mask) >> shift)
}

// get returns bit set at index i.
func (f *BloomFilter) get(i uint32) uint {
	idx, shift := i/64, i%64
	val := f.bits[idx]
	mask := uint64(1) << shift

	return uint((val & mask) >> shift)
}

// Reset clears the bloom filter.
func (f *BloomFilter) Reset() {
	for i := range f.bits {
		f.bits[i] = 0
	}
}
