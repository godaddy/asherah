package metastore_test

import (
	"testing"

	"github.com/aws/aws-sdk-go-v2/service/dynamodb"
	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
)

func TestNew(t *testing.T) {
	db, err := metastore.NewDynamoDB()
	assert.NoError(t, err)

	assert.NotNil(t, db.GetClient())
}

func TestNew_WithDynamoDBClient(t *testing.T) {
	client := &MockClient{}
	db, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
	assert.NoError(t, err)

	assert.Equal(t, client, db.GetClient(), "client should be the same as the one passed in")
}

func TestNew_WithTableName(t *testing.T) {
	table := "DummyTable"
	db, err := metastore.NewDynamoDB(metastore.WithTableName(table))
	assert.NoError(t, err)

	assert.Equal(t, table, db.GetTableName())
}

func TestNew_DefaultTableName(t *testing.T) {
	defaultTableName := "EncryptionKey"

	db, err := metastore.NewDynamoDB()
	assert.NoError(t, err)

	assert.Equal(t, defaultTableName, db.GetTableName())
}

func TestNew_WithRegionSuffix(t *testing.T) {
	client := &MockClient{}
	client.On("Options").Return(dynamodb.Options{Region: "some-region"})

	metastore, err := metastore.NewDynamoDB(
		metastore.WithDynamoDBClient(client),
		metastore.WithRegionSuffix(true),
	)
	assert.NoError(t, err)

	assert.Equal(t, "some-region", metastore.GetRegionSuffix())

	client.AssertExpectations(t)
}
