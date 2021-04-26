package appencryption

import (
	"testing"

	"github.com/stretchr/testify/assert"
)

func Test_NewPartition(t *testing.T) {
	partition := newPartition("partid", "service", "product")

	assert.NotNil(t, partition)
}

func TestDefaultPartition_SystemKeyID(t *testing.T) {
	partition := newPartition("partid", "service", "product")

	assert.Equal(t, "_SK_service_product", partition.SystemKeyID())
}

func TestDefaultPartition_IntermediateKeyID(t *testing.T) {
	partition := newPartition("partid", "service", "product")

	assert.Equal(t, "_IK_partid_service_product", partition.IntermediateKeyID())
}

func TestDefaultPartition_IsValidIntermediateKeyID(t *testing.T) {
	partition := newPartition("partid", "service", "product")

	assert.True(t, partition.IsValidIntermediateKeyID("_IK_partid_service_product"))
}

func Test_NewSuffixedPartition(t *testing.T) {
	partition := newSuffixedPartition("partid", "service", "product", "suffix")

	assert.NotNil(t, partition)
}

func TestSuffixPartition_SystemKeyID(t *testing.T) {
	partition := newSuffixedPartition("partid", "service", "product", "suffix")

	assert.Equal(t, "_SK_service_product_suffix", partition.SystemKeyID())
}

func TestSuffixPartition_IntermediateKeyID(t *testing.T) {
	partition := newSuffixedPartition("partid", "service", "product", "suffix")

	assert.Equal(t, "_IK_partid_service_product_suffix", partition.IntermediateKeyID())
}

func TestSuffixPartition_IsValidIntermediateKeyID(t *testing.T) {
	partition := newSuffixedPartition("partid", "service", "product", "suffix")

	assert.True(t, partition.IsValidIntermediateKeyID("_IK_partid_service_product_suffix"))
	assert.True(
		t,
		partition.IsValidIntermediateKeyID("_IK_partid_service_product"),
		"key IDs without suffixes should be valid to maintain backwards compatibility",
	)

	assert.True(
		t,
		partition.IsValidIntermediateKeyID("_IK_partid_service_product_othersuffix"),
		"key IDs with differing suffixes should be valid to allow cross-region operations",
	)
}
