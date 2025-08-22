//go:build !nodeprecated

package kms

import (
	"github.com/godaddy/asherah/go/appencryption"
	awsV1kms "github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms"
)

// KMS Metrics Counters & Timers.
var (
	_ appencryption.KeyManagementService = (*AWSKMS)(nil)
)

// KMS is implemented by the client in the kms package from the AWS SDK.
// We only use a subset of methods defined below.
//
// Deprecated: AWS SDK v1 reached end-of-life July 31, 2025. Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms instead.
type KMS = awsV1kms.KMS

// AWSKMSClient contains a KMS client and region information used for
// encrypting a key in KMS.
//
// Deprecated: AWS SDK v1 reached end-of-life July 31, 2025. Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms instead.
type AWSKMSClient = awsV1kms.AWSKMSClient

// AWSKMS implements the KeyManagementService interface and handles
// encryption/decryption in KMS.
//
// Deprecated: AWS SDK v1 reached end-of-life July 31, 2025. Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms instead.
type AWSKMS = awsV1kms.AWSKMS

// NewAWS returns a new AWSKMS used for encrypting/decrypting
// keys with a master key.
//
// Deprecated: AWS SDK v1 reached end-of-life July 31, 2025. Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms instead.
func NewAWS(crypto appencryption.AEAD, preferredRegion string, arnMap map[string]string) (*AWSKMS, error) {
	return awsV1kms.NewAWS(crypto, preferredRegion, arnMap)
}
