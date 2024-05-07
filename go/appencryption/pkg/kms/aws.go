package kms

import (
	"github.com/godaddy/asherah/go/appencryption"
	awsV1kms "github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms"
)

// KMS Metrics Counters & Timers
var (
	_ appencryption.KeyManagementService = (*AWSKMS)(nil)
)

// KMS is implemented by the client in the kms package from the AWS SDK.
// We only use a subset of methods defined below.
//
// DEPRECATED: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms.KMS instead.
type KMS = awsV1kms.KMS

// AWSKMSClient contains a KMS client and region information used for
// encrypting a key in KMS.
//
// DEPRECATED: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms.AWSKMSClient instead.
type AWSKMSClient = awsV1kms.AWSKMSClient

// AWSKMS implements the KeyManagementService interface and handles
// encryption/decryption in KMS.
//
// DEPRECATED: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms.AWSKMS instead.
type AWSKMS = awsV1kms.AWSKMS

// NewAWS returns a new AWSKMS used for encrypting/decrypting
// keys with a master key.
//
// DEPRECATED: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms.NewAWS instead.
func NewAWS(crypto appencryption.AEAD, preferredRegion string, arnMap map[string]string) (*AWSKMS, error) {
	return awsV1kms.NewAWS(crypto, preferredRegion, arnMap)
}
