package persistence

import (
	"context"
	"encoding/base64"
	"errors"
	"fmt"
	"strconv"
	"time"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/awserr"
	"github.com/aws/aws-sdk-go/aws/client"
	"github.com/aws/aws-sdk-go/aws/request"
	"github.com/aws/aws-sdk-go/service/dynamodb"
	"github.com/aws/aws-sdk-go/service/dynamodb/dynamodbattribute"
	"github.com/aws/aws-sdk-go/service/dynamodb/expression"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
)

const (
	defaultTableName = "EncryptionKey"
	partitionKey     = "Id"
	sortKey          = "Created"
	keyRecord        = "KeyRecord"
)

var (
	// DynamoDB metastore metrics.
	loadDynamoDBTimer       = metrics.GetOrRegisterTimer("ael.metastore.dynamodb.load", nil)
	loadLatestDynamoDBTimer = metrics.GetOrRegisterTimer("ael.metastore.dynamodb.loadlatest", nil)
	storeDynamoDBTimer      = metrics.GetOrRegisterTimer("ael.metastore.dynamodb.store", nil)
)

// ConfigProvider is an alias for the AWS SDK ConfigProvider interface.
type ConfigProvider = client.ConfigProvider

// DynamoDBMetastore implements the Metastore interface.
type DynamoDBMetastore struct {
	svc          DynamoDBClientAPI
	regionSuffix string
	tableName    string
}

// GetRegionSuffix returns the DynamoDB region suffix or blank if not configured.
func (d *DynamoDBMetastore) GetRegionSuffix() string {
	return d.regionSuffix
}

// GetTableName returns the DynamoDB table name.
func (d *DynamoDBMetastore) GetTableName() string {
	return d.tableName
}

// DynamoDBClientAPI is an interface that defines the methods used from the DynamoDB client.
type DynamoDBClientAPI interface {
	GetItemWithContext(aws.Context, *dynamodb.GetItemInput, ...request.Option) (*dynamodb.GetItemOutput, error)
	PutItemWithContext(aws.Context, *dynamodb.PutItemInput, ...request.Option) (*dynamodb.PutItemOutput, error)
	QueryWithContext(aws.Context, *dynamodb.QueryInput, ...request.Option) (*dynamodb.QueryOutput, error)
}

// GetClient returns the DynamoDB client.
func (d *DynamoDBMetastore) GetClient() DynamoDBClientAPI {
	return d.svc
}

// DynamoDBMetastoreOption is used to configure additional options in a DynamoDBMetastore.
type DynamoDBMetastoreOption func(d *DynamoDBMetastore, p ConfigProvider)

// WithDynamoDBRegionSuffix configures the DynamoDBMetastore to use a regional suffix for
// all writes. This feature should be enabled when using DynamoDB global tables to avoid
// write conflicts arising from the "last writer wins" method of conflict resolution.
func WithDynamoDBRegionSuffix(enabled bool) DynamoDBMetastoreOption {
	return func(d *DynamoDBMetastore, p ConfigProvider) {
		if enabled {
			config := p.ClientConfig(dynamodb.EndpointsID)
			d.regionSuffix = *config.Config.Region
		}
	}
}

// WithTableName configures the DynamoDBMetastore to use the specified table name.
func WithTableName(table string) DynamoDBMetastoreOption {
	return func(d *DynamoDBMetastore, p ConfigProvider) {
		if len(table) > 0 {
			d.tableName = table
		}
	}
}

// WithClient configures the DynamoDBMetastore to use the provided client.
//
// This is useful for testing or when you want to use a custom DynamoDB client.
func WithClient(c DynamoDBClientAPI) DynamoDBMetastoreOption {
	return func(d *DynamoDBMetastore, p ConfigProvider) {
		d.svc = c
	}
}

// NewDynamoDBMetastore creates a new DynamoDBMetastore with the provided session and options.
func NewDynamoDBMetastore(sess ConfigProvider, opts ...DynamoDBMetastoreOption) *DynamoDBMetastore {
	d := &DynamoDBMetastore{
		svc:       dynamodb.New(sess),
		tableName: defaultTableName,
	}

	for _, opt := range opts {
		opt(d, sess)
	}

	return d
}

func parseResult(av *dynamodb.AttributeValue) (*appencryption.EnvelopeKeyRecord, error) {
	var en appencryption.EnvelopeKeyRecord
	if err := dynamodbattribute.Unmarshal(av, &en); err != nil {
		return nil, fmt.Errorf("failed to unmarshal record: %w", err)
	}

	return &en, nil
}

// Load returns the key matching the keyID and created times provided. The envelope
// will be nil if it does not exist in the metastore.
func (d *DynamoDBMetastore) Load(ctx context.Context, keyID string, created int64) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadDynamoDBTimer.UpdateSince(time.Now())

	proj := expression.NamesList(expression.Name(keyRecord))

	expr, err := expression.NewBuilder().WithProjection(proj).Build()
	if err != nil {
		return nil, fmt.Errorf("dynamodb expression error: %w", err)
	}

	res, err := d.svc.GetItemWithContext(ctx, &dynamodb.GetItemInput{
		ExpressionAttributeNames: expr.Names(),
		Key: map[string]*dynamodb.AttributeValue{
			partitionKey: {S: &keyID},
			sortKey:      {N: aws.String(strconv.FormatInt(created, 10))},
		},
		ProjectionExpression: expr.Projection(),
		TableName:            aws.String(d.tableName),
		ConsistentRead:       aws.Bool(true), // always use strong consistency
	})
	if err != nil {
		return nil, fmt.Errorf("metastore error: %w", err)
	}

	if res.Item == nil {
		return nil, nil
	}

	return parseResult(res.Item[keyRecord])
}

// LoadLatest returns the newest record matching the keyID.
// The return value will be nil if not already present.
func (d *DynamoDBMetastore) LoadLatest(ctx context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadLatestDynamoDBTimer.UpdateSince(time.Now())

	cond := expression.Key(partitionKey).Equal(expression.Value(keyID))
	proj := expression.NamesList(expression.Name(keyRecord))

	expr, err := expression.NewBuilder().WithKeyCondition(cond).WithProjection(proj).Build()
	if err != nil {
		return nil, fmt.Errorf("dynamodb expression error: %w", err)
	}

	// Have to use query api to use limit and reverse sort order
	res, err := d.svc.QueryWithContext(ctx, &dynamodb.QueryInput{
		ConsistentRead:            aws.Bool(true), // always use strong consistency
		ExpressionAttributeNames:  expr.Names(),
		ExpressionAttributeValues: expr.Values(),
		KeyConditionExpression:    expr.KeyCondition(),
		Limit:                     aws.Int64(1), // limit 1
		ProjectionExpression:      expr.Projection(),
		ScanIndexForward:          aws.Bool(false), // sorts descending
		TableName:                 aws.String(d.tableName),
	})
	if err != nil {
		return nil, err
	}

	if len(res.Items) == 0 {
		return nil, nil
	}

	return parseResult(res.Items[0][keyRecord])
}

// DynamoDBEnvelope is used to convert the EncryptedKey to a Base64 encoded string
// to save in DynamoDB.
type DynamoDBEnvelope struct {
	Revoked       bool                   `json:"Revoked,omitempty"`
	Created       int64                  `json:"Created"`
	EncryptedKey  string                 `json:"Key"`
	ParentKeyMeta *appencryption.KeyMeta `json:"ParentKeyMeta,omitempty"`
}

// Store attempts to insert the key into the metastore if one is not
// already present. If a key exists, the method will return false. If
// one is not present, the value will be inserted and we return true.
func (d *DynamoDBMetastore) Store(ctx context.Context, keyID string, created int64, envelope *appencryption.EnvelopeKeyRecord) (bool, error) {
	defer storeDynamoDBTimer.UpdateSince(time.Now())

	en := &DynamoDBEnvelope{
		Revoked:       envelope.Revoked,
		Created:       envelope.Created,
		EncryptedKey:  base64.StdEncoding.EncodeToString(envelope.EncryptedKey),
		ParentKeyMeta: envelope.ParentKeyMeta,
	}

	av, err := dynamodbattribute.MarshalMap(&en)
	if err != nil {
		return false, fmt.Errorf("failed to marshal envelope: %w", err)
	}

	// Note conditional expression using attribute_not_exists has special semantics. Can be used on partition OR
	// sort key alone to guarantee primary key uniqueness. It automatically checks for existence of this item's
	// composite primary key and if it contains the specified attribute name, either of which is inherently
	// required.
	_, err = d.svc.PutItemWithContext(ctx, &dynamodb.PutItemInput{
		Item: map[string]*dynamodb.AttributeValue{
			partitionKey: {S: &keyID},
			sortKey:      {N: aws.String(strconv.FormatInt(created, 10))},
			keyRecord:    {M: av},
		},
		TableName:           aws.String(d.tableName),
		ConditionExpression: aws.String("attribute_not_exists(" + partitionKey + ")"),
	})
	if err != nil {
		var awsErr awserr.Error
		if errors.As(err, &awsErr) && awsErr.Code() == dynamodb.ErrCodeConditionalCheckFailedException {
			return false, fmt.Errorf("attempted to create duplicate key: %s, %d: %w", keyID, created, err)
		}

		return false, fmt.Errorf("error storing key  key: %s, %d: %w", keyID, created, err)
	}

	return true, nil
}
