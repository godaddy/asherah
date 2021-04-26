package appencryption

import (
	"fmt"
	"strings"
)

func newPartition(partition, service, product string) defaultPartition {
	return defaultPartition{
		id:      partition,
		service: service,
		product: product,
	}
}

// partition returns the SystemKey name and IntermediateKey name for the
// current partition id.
type partition interface {
	SystemKeyID() string
	IntermediateKeyID() string
	IsValidIntermediateKeyID(id string) bool
}

// defaultPartition is the default implementation for partition naming.
type defaultPartition struct {
	id      string
	service string
	product string
}

// SystemKeyID returns the system key name for the product/service.
func (p defaultPartition) SystemKeyID() string {
	return fmt.Sprintf("_SK_%s_%s", p.service, p.product)
}

// IntermediateKeyID returns the intermediate key name for the product/service.
func (p defaultPartition) IntermediateKeyID() string {
	return fmt.Sprintf("_IK_%s_%s_%s", p.id, p.service, p.product)
}

// IsValidIntermediateKeyID ensures the given ID is a valid intermediate key ID for this partition
func (p defaultPartition) IsValidIntermediateKeyID(id string) bool {
	return id == p.IntermediateKeyID()
}

func newSuffixedPartition(partition, service, product, suffix string) suffixedPartition {
	return suffixedPartition{
		defaultPartition: defaultPartition{
			id:      partition,
			service: service,
			product: product,
		},
		suffix: suffix,
	}
}

type suffixedPartition struct {
	defaultPartition
	suffix string
}

// SystemKeyID returns the system key name for the product/service.
func (p suffixedPartition) SystemKeyID() string {
	return fmt.Sprintf("_SK_%s_%s_%s", p.service, p.product, p.suffix)
}

// IntermediateKeyID returns the intermediate key name for the product/service.
func (p suffixedPartition) IntermediateKeyID() string {
	return fmt.Sprintf("_IK_%s_%s_%s_%s", p.id, p.service, p.product, p.suffix)
}

// IsValidIntermediateKeyID ensures the given ID is a valid intermediate key ID for this partition
func (p suffixedPartition) IsValidIntermediateKeyID(id string) bool {
	return id == p.IntermediateKeyID() || strings.Index(id, p.defaultPartition.IntermediateKeyID()) == 0
}
