package kms_test

import (
	"context"
	"testing"

	"github.com/aws/aws-sdk-go-v2/aws"
	awskms "github.com/aws/aws-sdk-go-v2/service/kms"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms"
)

type MockCrypto struct {
	mock.Mock
}

func (c *MockCrypto) NonceByteSize() int {
	ret := c.Called()

	var nonceSize int
	if b := ret.Get(0); b != nil {
		nonceSize = b.(int)
	}

	return nonceSize
}

func (c *MockCrypto) KeyByteSize() int {
	ret := c.Called()

	var keySize int
	if b := ret.Get(0); b != nil {
		keySize = b.(int)
	}

	return keySize
}

func (c *MockCrypto) Encrypt(data, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

func (c *MockCrypto) Decrypt(data []byte, key []byte) ([]byte, error) {
	ret := c.Called(data, key)

	var bytes []byte
	if b := ret.Get(0); b != nil {
		bytes = b.([]byte)
	}

	return bytes, ret.Error(1)
}

type MockClient struct {
	mock.Mock
}

func (c *MockClient) Encrypt(ctx context.Context, params *awskms.EncryptInput, optFns ...func(*awskms.Options)) (*awskms.EncryptOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*awskms.EncryptOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) Decrypt(ctx context.Context, params *awskms.DecryptInput, optFns ...func(*awskms.Options)) (*awskms.DecryptOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*awskms.DecryptOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) GenerateDataKey(ctx context.Context, params *awskms.GenerateDataKeyInput, optFns ...func(*awskms.Options)) (*awskms.GenerateDataKeyOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*awskms.GenerateDataKeyOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

var _ kms.AWSClient = (*MockClient)(nil)

func TestBuilder(t *testing.T) {
	region := "us-west-2"
	regionArnMap := map[string]string{
		region: "arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012",
	}

	crypto := &MockCrypto{}

	customCfg := aws.Config{
		Region: "us-west-2",
	}

	client := &MockClient{}

	builder := kms.NewBuilder(crypto, regionArnMap)
	builder.WithAWSConfig(customCfg)
	builder.WithKMSFactory(func(cfg aws.Config, optFns ...func(*awskms.Options)) kms.AWSClient {
		// assert that the custom config is passed to the factory
		assert.Equal(t, customCfg, cfg)

		return client
	})

	awsKMS, err := builder.Build()
	assert.Nil(t, err)
	assert.NotNil(t, awsKMS)
	assert.Equal(t, region, awsKMS.PreferredRegion())
}

func TestBuilder_MultiRegion(t *testing.T) {
	preferred := "us-east-1"
	regionArnMap := map[string]string{
		preferred:   "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
		"us-west-2": "arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012",
	}

	crypto := &MockCrypto{}

	awsKMS, err := kms.NewBuilder(crypto, regionArnMap).
		WithPreferredRegion(preferred).
		Build()

	assert.Nil(t, err)
	assert.NotNil(t, awsKMS)
	assert.Equal(t, preferred, awsKMS.PreferredRegion())
}

func TestBuilder_MultiRegion_MissingPreferredRegion(t *testing.T) {
	regionArnMap := map[string]string{
		"us-west-2": "arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012",
		"us-east-1": "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
	}

	crypto := &MockCrypto{}

	builder := kms.NewBuilder(crypto, regionArnMap)

	_, err := builder.Build()
	assert.ErrorContains(t, err, "preferred region must be set when using multiple regions")
}
