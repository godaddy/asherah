package metastore_test

import (
	"context"
	"strconv"
	"testing"

	"github.com/aws/aws-sdk-go-v2/service/dynamodb"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb/types"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
)

type MockClient struct {
	mock.Mock
}

func (c *MockClient) GetItem(ctx context.Context, params *dynamodb.GetItemInput, optFns ...func(*dynamodb.Options)) (*dynamodb.GetItemOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*dynamodb.GetItemOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) PutItem(ctx context.Context, params *dynamodb.PutItemInput, optFns ...func(*dynamodb.Options)) (*dynamodb.PutItemOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*dynamodb.PutItemOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) Query(ctx context.Context, params *dynamodb.QueryInput, optFns ...func(*dynamodb.Options)) (*dynamodb.QueryOutput, error) {
	args := c.Called(ctx, params, optFns)

	if out := args.Get(0); out != nil {
		return out.(*dynamodb.QueryOutput), args.Error(1)
	}

	return nil, args.Error(1)
}

func (c *MockClient) Options() dynamodb.Options {
	args := c.Called()
	return args.Get(0).(dynamodb.Options)
}

func TestMetastore_Load(t *testing.T) {
	fake := getFakeOutputItem()

	tests := []struct {
		name   string
		output *dynamodb.GetItemOutput
		err    error

		expected    *appencryption.EnvelopeKeyRecord
		expectedErr error
	}{
		{
			name: "Success",
			output: &dynamodb.GetItemOutput{
				Item: fake,
			},
			err: nil,

			expected:    getFakeEnvelopeKeyRecord(),
			expectedErr: nil,
		},
		{
			name:   "DynamoDB error",
			output: nil,
			err:    assert.AnError,

			expected:    nil,
			expectedErr: assert.AnError,
		},
		{
			name: "No item found",
			output: &dynamodb.GetItemOutput{
				Item: nil,
			},
			err: nil,

			expected:    nil,
			expectedErr: nil, // No error expected
		},
		{
			name: "Invalid item",
			output: &dynamodb.GetItemOutput{
				Item: map[string]types.AttributeValue{
					"Id": &types.AttributeValueMemberN{
						Value: "testKey",
					},
				},
			},
			err: nil,

			expected:    nil,
			expectedErr: metastore.ItemDecodeError,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			client := &MockClient{}
			db, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
			assert.NoError(t, err)

			client.On("GetItem", mock.Anything, mock.Anything, mock.Anything).Return(tt.output, tt.err)

			envelope, err := db.Load(context.Background(), "testKey", 0)
			assert.EqualValues(t, tt.expected, envelope)
			assert.ErrorIs(t, err, tt.expectedErr)

			client.AssertExpectations(t)
		})
	}
}

func getFakeOutputItem() map[string]types.AttributeValue {
	env := getFakeEnvelopeKeyRecord()

	base64 := "YmFzZTY0" // base64 encoded "base64"

	return map[string]types.AttributeValue{
		// Partition key
		"Id": &types.AttributeValueMemberS{
			Value: "testKey",
		},

		// Sort key
		"Created": &types.AttributeValueMemberN{
			Value: strconv.FormatInt(env.Created, 10),
		},

		// Envelope key record
		"KeyRecord": &types.AttributeValueMemberM{
			Value: map[string]types.AttributeValue{
				"Key": &types.AttributeValueMemberS{
					Value: base64,
				},
				"Created": &types.AttributeValueMemberN{
					Value: strconv.FormatInt(env.Created, 10),
				},
				"ParentKeyMeta": &types.AttributeValueMemberM{
					Value: map[string]types.AttributeValue{
						"KeyId": &types.AttributeValueMemberS{
							Value: env.ParentKeyMeta.ID,
						},
						"Created": &types.AttributeValueMemberN{
							Value: strconv.FormatInt(env.ParentKeyMeta.Created, 10),
						},
					},
				},
			},
		},
	}
}

func getFakeEnvelopeKeyRecord() *appencryption.EnvelopeKeyRecord {
	return &appencryption.EnvelopeKeyRecord{
		ID:           "testKey",
		Created:      1234567890,
		EncryptedKey: []byte("base64"),
		ParentKeyMeta: &appencryption.KeyMeta{
			ID:      "parentKeyId",
			Created: 1234567889,
		},
	}
}

func TestMetastore_LoadLatest(t *testing.T) {
	fake := getFakeOutputItem()

	tests := []struct {
		name   string
		output *dynamodb.QueryOutput
		err    error

		expected    *appencryption.EnvelopeKeyRecord
		expectedErr error
	}{
		{
			name: "Success",
			output: &dynamodb.QueryOutput{
				Items: []map[string]types.AttributeValue{fake},
			},

			expected:    getFakeEnvelopeKeyRecord(),
			expectedErr: nil,
		},
		{
			name:   "DynamoDB error",
			output: nil,
			err:    assert.AnError,

			expected:    nil,
			expectedErr: assert.AnError,
		},
		{
			name: "No item found",
			output: &dynamodb.QueryOutput{
				Items: nil,
			},

			expected:    nil,
			expectedErr: nil, // No error expected
		},
		{
			name: "Invalid item",
			output: &dynamodb.QueryOutput{
				Items: []map[string]types.AttributeValue{
					{
						"Id": &types.AttributeValueMemberN{
							Value: "testKey",
						},
					},
				},
			},

			expected:    nil,
			expectedErr: metastore.ItemDecodeError,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			client := &MockClient{}
			db, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
			assert.NoError(t, err)

			client.On("Query", mock.Anything, mock.Anything, mock.Anything).Return(tt.output, tt.err)

			envelope, err := db.LoadLatest(context.Background(), "testKey")
			assert.EqualValues(t, tt.expected, envelope)
			assert.ErrorIs(t, err, tt.expectedErr)

			client.AssertExpectations(t)
		})
	}
}

func TestMetastore_Store(t *testing.T) {
	dupErr := &types.ConditionalCheckFailedException{}

	tests := []struct {
		name string
		err  error

		okExpected  bool
		expectedErr error
	}{
		{
			name: "Success",
			err:  nil,

			okExpected:  true,
			expectedErr: nil,
		},
		{
			name: "DynamoDB duplicate key error",
			err:  dupErr,

			okExpected:  false,
			expectedErr: dupErr,
		},
		{
			name: "DynamoDB unknown error",
			err:  assert.AnError,

			okExpected:  false,
			expectedErr: assert.AnError,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			client := &MockClient{}
			db, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
			assert.NoError(t, err)

			client.On("PutItem", mock.Anything, mock.Anything, mock.Anything).Return(nil, tt.err)

			ekr := getFakeEnvelopeKeyRecord()

			ok, err := db.Store(context.Background(), ekr.ID, ekr.Created, ekr)
			assert.Equal(t, tt.okExpected, ok)
			assert.ErrorIs(t, err, tt.expectedErr)

			client.AssertExpectations(t)
		})
	}
}
