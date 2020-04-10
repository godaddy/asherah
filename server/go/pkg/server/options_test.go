package server

import (
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func Test_RegionMap(t *testing.T) {
	tests := []struct {
		name     string
		value    string
		expected RegionMap
		isErr    bool
	}{
		{
			name:     "single region",
			value:    "region-1=aws-1",
			expected: RegionMap{"region-1": "aws-1"},
		},
		{
			name:     "multi-region",
			value:    "region-1=aws-1,region-2=aws-2",
			expected: RegionMap{"region-1": "aws-1", "region-2": "aws-2"},
		},
		{name: "trailing comma", value: "region-1=arn-1,", isErr: true},
		{name: "missing arn", value: "region-1=", isErr: true},
		{name: "region only", value: "region-1", isErr: true},
	}

	for i := range tests {
		test := tests[i]
		t.Run(test.name, func(tt *testing.T) {
			regions := make(RegionMap)
			err := regions.UnmarshalFlag(test.value)
			if test.isErr {
				require.Error(tt, err)
				return
			}

			assert.Equal(tt, test.expected, regions)
		})
	}
}
