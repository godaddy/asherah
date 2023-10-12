package internal_test

import (
	"testing"

	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption/pkg/cache/internal"
)

type HashSuite struct {
	suite.Suite
}

func TestHashSuite(t *testing.T) {
	suite.Run(t, new(HashSuite))
}

type hashable struct{}

func (h hashable) Sum64() uint64 {
	return 42
}

func (suite *HashSuite) TestComputeHash() {
	tests := []struct {
		input    interface{}
		expected uint64
	}{
		{input: -1, expected: 0x8cf51a8bfca3883d},
		{input: int8(-8), expected: 0xc49d767d487ba59e},
		{input: int16(-16), expected: 0xbff576369e732626},
		{input: int32(-32), expected: 0xfc0775b30ed9a536},
		{input: int64(-64), expected: 0xd1bdb52ab00c8d2},
		{input: uint(1), expected: 0x89cd31291d2aefa4},
		{input: uint8(8), expected: 0x4cfad6c24f7bf87d},
		{input: uint16(16), expected: 0x4cd037050129dd05},
		{input: uint32(32), expected: 0x4dcff574d71681d5},
		{input: uint64(64), expected: 0x6779ba74e3ecc205},
		{input: uintptr(uint64(64)), expected: 0x6779ba74e3ecc205},
		{input: float32(2.5), expected: 0x4cb8767f9d714215},
		{input: float64(2.5), expected: 0xa8ba2032280e4061},
		{input: true, expected: 1},
		{input: "1", expected: 0xaf63ac4c86019afc},
		{input: hashable{}, expected: 42},
	}

	for i, test := range tests {
		i := i
		suite.Assert().Equal(test.expected, internal.ComputeHash(test.input), "test %d", i)
	}
}

func (suite *HashSuite) TestComputeHashForPointer() {
	input := make([]byte, 0)

	h := internal.ComputeHash(input)
	suite.Assert().NotEqual(uint64(0), h)
}
