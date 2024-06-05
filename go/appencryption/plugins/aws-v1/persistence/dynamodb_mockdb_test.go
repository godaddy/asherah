package persistence_test

import (
	"context"
	"testing"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/request"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/aws/aws-sdk-go/service/dynamodb"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence"
)

type mockDynamoDB struct {
	mock.Mock
}

func (m *mockDynamoDB) GetItemWithContext(ctx aws.Context, input *dynamodb.GetItemInput, opts ...request.Option) (*dynamodb.GetItemOutput, error) {
	args := m.Called(ctx, input, opts)
	return args.Get(0).(*dynamodb.GetItemOutput), args.Error(1)
}

func (m *mockDynamoDB) PutItemWithContext(ctx aws.Context, input *dynamodb.PutItemInput, opts ...request.Option) (*dynamodb.PutItemOutput, error) {
	args := m.Called(ctx, input, opts)
	return args.Get(0).(*dynamodb.PutItemOutput), args.Error(1)
}

func (m *mockDynamoDB) QueryWithContext(ctx aws.Context, input *dynamodb.QueryInput, opts ...request.Option) (*dynamodb.QueryOutput, error) {
	args := m.Called(ctx, input, opts)
	return args.Get(0).(*dynamodb.QueryOutput), args.Error(1)
}

func TestDynamoDBMetastore_WithClient(t *testing.T) {
	sess := getTestSession(t)

	client := &mockDynamoDB{}
	db := persistence.NewDynamoDBMetastore(sess, persistence.WithClient(client))

	assert.Equal(t, client, db.GetClient(), "client should be the same as the one passed in")
}

func getTestSession(t *testing.T) *session.Session {
	sess, err := session.NewSession(&aws.Config{
		Region:   aws.String("us-west-2"),
		Endpoint: aws.String("http://localhost:8000"),
	})
	require.NoError(t, err)

	return sess
}

func TestDynamoDBMetastore_Load(t *testing.T) {
	ctx := context.Background()

	sess := getTestSession(t)

	client := &mockDynamoDB{}
	db := persistence.NewDynamoDBMetastore(sess, persistence.WithClient(client))

	client.On("GetItemWithContext", ctx, mock.Anything, mock.Anything).Return(getItemOutput_WithDummyItem(), nil)

	envelope, err := db.Load(ctx, "testKey", 0)
	require.NoError(t, err)

	assert.NotNil(t, envelope)
	assert.Equal(t, "parentKeyId", envelope.ParentKeyMeta.ID)

	client.AssertExpectations(t)
}

func getItemOutput_WithDummyItem() *dynamodb.GetItemOutput {
	return &dynamodb.GetItemOutput{
		Item: getDummyItem(),
	}
}

func getDummyItem() map[string]*dynamodb.AttributeValue {
	base64 := "YmFzZTY0" // base64 encoded "base64"

	return map[string]*dynamodb.AttributeValue{
		"KeyRecord": {
			M: map[string]*dynamodb.AttributeValue{
				"Key": {
					S: aws.String(base64),
				},
				"Created": {
					N: aws.String("1234567890"),
				},
				"ParentKeyMeta": {
					M: map[string]*dynamodb.AttributeValue{
						"KeyId": {
							S: aws.String("parentKeyId"),
						},
						"Created": {
							N: aws.String("1234567889"),
						},
					},
				},
			},
		},
	}
}

func TestDynamoDBMetastore_LoadLatest(t *testing.T) {
	ctx := context.Background()

	sess := getTestSession(t)

	client := &mockDynamoDB{}
	db := persistence.NewDynamoDBMetastore(sess, persistence.WithClient(client))

	dummy := getDummyItem()
	queryResponse := &dynamodb.QueryOutput{
		Items: []map[string]*dynamodb.AttributeValue{dummy},
	}

	client.On("QueryWithContext", ctx, mock.Anything, mock.Anything).Return(queryResponse, nil)

	envelope, err := db.LoadLatest(ctx, "testKey")
	require.NoError(t, err)

	assert.NotNil(t, envelope)
	assert.Equal(t, "parentKeyId", envelope.ParentKeyMeta.ID)

	client.AssertExpectations(t)
}

func TestDynamoDBMetastore_Store(t *testing.T) {
	ctx := context.Background()

	sess := getTestSession(t)

	client := &mockDynamoDB{}
	db := persistence.NewDynamoDBMetastore(sess, persistence.WithClient(client))

	client.On("PutItemWithContext", ctx, mock.Anything, mock.Anything).Return(&dynamodb.PutItemOutput{}, nil)

	envelope := &appencryption.EnvelopeKeyRecord{
		ID:           "testKey",
		EncryptedKey: []byte("base64"),
		Created:      1234567890,
		ParentKeyMeta: &appencryption.KeyMeta{
			ID:      "parentKeyId",
			Created: 1234567889,
		},
	}

	inserted, err := db.Store(ctx, "testKey", 1234567890, envelope)
	require.NoError(t, err)
	assert.True(t, inserted)

	client.AssertExpectations(t)
}
