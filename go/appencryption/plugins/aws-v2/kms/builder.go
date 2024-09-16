package kms

import (
	"context"
	"errors"
	"fmt"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/kms"

	"github.com/godaddy/asherah/go/appencryption"
)

// KMSFactory is a function that creates a new AWS KMS client.
type KMSFactory func(cfg aws.Config, optFns ...func(*kms.Options)) AWSClient

// DefaultKMSFactory wraps kms.NewFromConfig.
// It is used by default when creating new AWS KMS clients with the Builder.
func DefaultKMSFactory(cfg aws.Config, optFns ...func(*kms.Options)) AWSClient {
	return kms.NewFromConfig(cfg, optFns...)
}

// Builder is used to build a new AWSKMS.
type Builder struct {
	arnMap map[string]string

	crypto appencryption.AEAD

	preferredRegion string

	factory KMSFactory

	cfg            aws.Config
	usingCustomCfg bool
}

// NewBuilder creates a new Builder with the given crypto and ARN map.
// Use the With* methods to configure the Builder, then call Build to create a new AWSKMS.
func NewBuilder(crypto appencryption.AEAD, arnMap map[string]string) *Builder {
	if len(arnMap) == 0 {
		panic("arnMap must contain at least one entry")
	}

	return &Builder{
		arnMap: arnMap,
		crypto: crypto,
	}
}

// WithPreferredRegion sets the preferred region for the AWSKMS.
//
// Required when using multiple regions.
func (b *Builder) WithPreferredRegion(region string) *Builder {
	b.preferredRegion = region
	return b
}

// WithKMSFactory sets the KMS factory for the AWSKMS.
// Default is to use kms.NewFromConfig.
//
// This is used for testing but is also useful for customizing the KMS client creation.
func (b *Builder) WithKMSFactory(factory KMSFactory) *Builder {
	b.factory = factory
	return b
}

// WithAWSConfig sets the AWS configuration for the AWSKMS when creating the clients.
// Default is to use the default AWS SDK configuration.
func (b *Builder) WithAWSConfig(cfg aws.Config) *Builder {
	b.cfg = cfg
	b.usingCustomCfg = true

	return b
}

// Build creates a new AWSKMS using the Builder configuration.
func (b *Builder) Build() (*AWSKMS, error) {
	if b.factory == nil {
		b.factory = DefaultKMSFactory
	}

	if !b.usingCustomCfg {
		cfg, err := config.LoadDefaultConfig(context.Background())
		if err != nil {
			return nil, fmt.Errorf("unable to load default AWS config: %w", err)
		}

		b.cfg = cfg
	}

	if b.preferredRegion == "" && len(b.arnMap) > 1 {
		return nil, errors.New("preferred region must be set when using multiple regions")
	}

	var clients []regionalClient

	for region, arn := range b.arnMap {
		cfg := b.cfg.Copy()
		cfg.Region = region

		client := regionalClient{
			Client:       b.factory(cfg),
			Region:       region,
			MasterKeyARN: arn,
		}

		// place the preferred region first in the list
		if region == b.preferredRegion {
			clients = append([]regionalClient{client}, clients...)
		} else {
			clients = append(clients, client)
		}
	}

	return &AWSKMS{
		clients: clients,
		crypto:  b.crypto,
	}, nil
}
