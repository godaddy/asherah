//go:build !nodeprecated

package persistence

import (
	awsV1Persistence "github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence"
)

// DynamoDBMetastore implements the Metastore interface.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.DynamoDBMetastore instead.
type DynamoDBMetastore = awsV1Persistence.DynamoDBMetastore

// DynamoDBMetastoreOption is used to configure additional options in a DynamoDBMetastore.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.DynamoDBMetastoreOption instead.
type DynamoDBMetastoreOption = awsV1Persistence.DynamoDBMetastoreOption

// WithDynamoDBRegionSuffix configures the DynamoDBMetastore to use a regional suffix for
// all writes. This feature should be enabled when using DynamoDB global tables to avoid
// write conflicts arising from the "last writer wins" method of conflict resolution.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.WithDynamoDBRegionSuffix instead.
func WithDynamoDBRegionSuffix(enabled bool) DynamoDBMetastoreOption {
	return awsV1Persistence.WithDynamoDBRegionSuffix(enabled)
}

// WithTableName configures the DynamoDBMetastore to use the specified table name.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.WithTableName instead.
func WithTableName(table string) DynamoDBMetastoreOption {
	return awsV1Persistence.WithTableName(table)
}

type DynamoDBClientAPI = awsV1Persistence.DynamoDBClientAPI

// WithClient configures the DynamoDBMetastore to use the specified DynamoDB client.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.WithClient instead.
func WithClient(c DynamoDBClientAPI) DynamoDBMetastoreOption {
	return awsV1Persistence.WithClient(c)
}

// NewDynamoDBMetastore returns a new DynamoDBMetastore.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.NewDynamoDBMetastore instead.
func NewDynamoDBMetastore(sess awsV1Persistence.ConfigProvider, opts ...DynamoDBMetastoreOption) *DynamoDBMetastore {
	return awsV1Persistence.NewDynamoDBMetastore(sess, opts...)
}

// DynamoDBEnvelope is used to convert the EncryptedKey to a Base64 encoded string to save in DynamoDB.
//
// Deprecated: Use github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence.DynamoDBEnvelope instead.
type DynamoDBEnvelope = awsV1Persistence.DynamoDBEnvelope
