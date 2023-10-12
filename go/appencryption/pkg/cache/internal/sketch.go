package internal

const sketchDepth = 4

// CountMinSketch is an implementation of count-min sketch with 4-bit counters.
// See http://dimacs.rutgers.edu/~graham/pubs/papers/cmsoft.pdf
type CountMinSketch struct {
	counters []uint64
	mask     uint32
}

// init initialize count-min sketch with the given width.
func (c *CountMinSketch) Init(width int) {
	// Need (width x 4 x 4) bits = width/4 x uint64
	size := nextPowerOfTwo(uint32(width)) >> 2
	if size < 1 {
		size = 1
	}

	c.mask = size - 1
	if len(c.counters) == int(size) {
		c.clear()
	} else {
		c.counters = make([]uint64, size)
	}
}

// Add increases counters associated with the given hash.
func (c *CountMinSketch) Add(h uint64) {
	h1, h2 := uint32(h), uint32(h>>32)

	for i := uint32(0); i < sketchDepth; i++ {
		idx, off := c.position(h1 + i*h2)
		c.inc(idx, (16*i)+off)
	}
}

// Estimate returns minimum value of counters associated with the given hash.
func (c *CountMinSketch) Estimate(h uint64) uint8 {
	h1, h2 := uint32(h), uint32(h>>32)

	var min uint8 = 0xFF

	for i := uint32(0); i < sketchDepth; i++ {
		idx, off := c.position(h1 + i*h2)

		count := c.val(idx, (16*i)+off)
		if count < min {
			min = count
		}
	}

	return min
}

// Reset divides all counters by two.
func (c *CountMinSketch) Reset() {
	for i, v := range c.counters {
		if v != 0 {
			c.counters[i] = (v >> 1) & 0x7777777777777777
		}
	}
}

func (c *CountMinSketch) position(h uint32) (idx uint32, off uint32) {
	idx = (h >> 2) & c.mask
	off = (h & 3) << 2

	return
}

// inc increases value at index idx.
func (c *CountMinSketch) inc(idx, off uint32) {
	v := c.counters[idx]

	if count := uint8(v>>off) & 0x0F; count < 15 {
		c.counters[idx] = v + (1 << off)
	}
}

// val returns value at index idx.
func (c *CountMinSketch) val(idx, off uint32) uint8 {
	v := c.counters[idx]
	return uint8(v>>off) & 0x0F
}

func (c *CountMinSketch) clear() {
	for i := range c.counters {
		c.counters[i] = 0
	}
}
