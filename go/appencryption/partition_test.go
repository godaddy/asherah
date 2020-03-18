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
