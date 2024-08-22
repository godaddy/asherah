package metastore

import (
	"context"
	"encoding/base64"
	"errors"
	"fmt"
	"strconv"
	"time"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/feature/dynamodb/attributevalue"
	"github.com/aws/aws-sdk-go-v2/feature/dynamodb/expression"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb/types"
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

// DynamoDBClient is an interface that defines the set of Amazon DynamoDB client operations required by this package.
type DynamoDBClient interface {
	GetItem(ctx context.Context, params *dynamodb.GetItemInput, optFns ...func(*dynamodb.Options)) (*dynamodb.GetItemOutput, error)
	PutItem(ctx context.Context, params *dynamodb.PutItemInput, optFns ...func(*dynamodb.Options)) (*dynamodb.PutItemOutput, error)
	Query(ctx context.Context, params *dynamodb.QueryInput, optFns ...func(*dynamodb.Options)) (*dynamodb.QueryOutput, error)
	Options() dynamodb.Options
}

// Option is a functional option for configuring the Metastore.
type Option func(*Metastore)

// WithRegionSuffix configures the Metastore for use with regional suffixes.
// This feature should be enabled when using DynamoDB global tables to avoid write conflicts arising from the
// "last writer wins" method of conflict resolution.
//
// When enabled, the region suffix can be retrieved using GetRegionSuffix.
func WithRegionSuffix(enabled bool) Option {
	return func(d *Metastore) {
		d.regionSuffixEnabled = enabled
	}
}

// WithTableName sets the DynamoDB table name for the Metastore.
// The default table name is "EncryptionKey".
func WithTableName(name string) Option {
	return func(d *Metastore) {
		if name != "" {
			d.tableName = name
		}
	}
}

// WithDynamoDBClient sets the DynamoDB client for the Metastore.
// Use this option to provide a custom client (and configuration) for the Metastore.
//
// Example:
//
//	client := dynamodb.NewFromConfig(cfg)
//	metastore, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
func WithDynamoDBClient(client DynamoDBClient) Option {
	return func(d *Metastore) {
		d.svc = client
	}
}

// Metastore implements the Metastore interface.
type Metastore struct {
	svc       DynamoDBClient
	tableName string

	regionSuffix        string
	regionSuffixEnabled bool
}

// NewDynamoDB returns a new DynamoDB-backed Metastore with the provided options.
func NewDynamoDB(opts ...Option) (*Metastore, error) {
	d := &Metastore{
		tableName: defaultTableName,
	}

	for _, opt := range opts {
		opt(d)
	}

	if d.svc == nil {
		client, err := newDefaultClient()
		if err != nil {
			return nil, err
		}

		d.svc = client
	}

	if d.regionSuffixEnabled {
		d.regionSuffix = d.svc.Options().Region
	}

	return d, nil
}

// newDefaultClient returns a new DynamoDB client with default configuration.
func newDefaultClient() (DynamoDBClient, error) {
	cfg, err := config.LoadDefaultConfig(context.Background())
	if err != nil {
		return nil, fmt.Errorf("unable to load default AWS config: %w", err)
	}

	return dynamodb.NewFromConfig(cfg), nil
}

// GetClient returns the underlying DynamoDBClient.
func (d *Metastore) GetClient() DynamoDBClient {
	return d.svc
}

// GetTableName returns the configured table name.
func (d *Metastore) GetTableName() string {
	return d.tableName
}

// GetRegionSuffix returns the region suffix if enabled.
func (d *Metastore) GetRegionSuffix() string {
	return d.regionSuffix
}

// Load returns the key matching the keyID and created times provided.
// The envelope key record is returned if found, otherwise both the key and error are nil.
func (d *Metastore) Load(ctx context.Context, keyID string, created int64) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadDynamoDBTimer.UpdateSince(time.Now())

	proj := expression.NamesList(expression.Name(keyRecord))

	expr, err := expression.NewBuilder().WithProjection(proj).Build()
	if err != nil {
		return nil, fmt.Errorf("dynamodb expression error: %w", err)
	}

	res, err := d.svc.GetItem(ctx, &dynamodb.GetItemInput{
		ExpressionAttributeNames: expr.Names(),
		Key: map[string]types.AttributeValue{
			partitionKey: &types.AttributeValueMemberS{Value: keyID},
			sortKey:      &types.AttributeValueMemberN{Value: strconv.FormatInt(created, 10)},
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

	return decodeItem(res.Item)
}

// metastoreItem represents a single item in the DynamoDB metastore.
type metastoreItem struct {
	ID        string    `dynamodbav:"Id"`
	Created   int64     `dynamodbav:"Created"`
	KeyRecord *envelope `dynamodbav:"KeyRecord"`
}

// envelope represents the envelope key record stored in the metastore.
type envelope struct {
	Revoked       bool     `dynamodbav:"Revoked,omitempty"`
	Created       int64    `dynamodbav:"Created"`
	EncryptedKey  string   `dynamodbav:"Key"`
	ParentKeyMeta *keyMeta `dynamodbav:"ParentKeyMeta,omitempty"`
}

// keyMeta contains the ID and Created timestamp for an encryption key.
type keyMeta struct {
	ID      string `dynamodbav:"KeyId"`
	Created int64  `dynamodbav:"Created"`
}

// decodeItem decodes a map of attribute values into an EnvelopeKeyRecord.
func decodeItem(m map[string]types.AttributeValue) (*appencryption.EnvelopeKeyRecord, error) {
	var item metastoreItem
	err := attributevalue.UnmarshalMap(m, &item)
	if err != nil {
		return nil, fmt.Errorf("failed to unmarshal record: %w", err)
	}

	en := item.KeyRecord
	if en == nil {
		return nil, fmt.Errorf("%w: unexpected nil envelope key record", ItemDecodeError)
	}

	encryptedKey, err := base64.StdEncoding.DecodeString(en.EncryptedKey)
	if err != nil {
		return nil, fmt.Errorf("failed to decode encrypted key: %w", err)
	}

	var km *appencryption.KeyMeta
	if en.ParentKeyMeta != nil {
		km = &appencryption.KeyMeta{
			ID:      en.ParentKeyMeta.ID,
			Created: en.ParentKeyMeta.Created,
		}
	}

	return &appencryption.EnvelopeKeyRecord{
		ID:            item.ID,
		Revoked:       en.Revoked,
		Created:       en.Created,
		EncryptedKey:  encryptedKey,
		ParentKeyMeta: km,
	}, nil
}

// LoadLatest returns the newest record matching the keyID.
// The return value will be nil if not already present.
func (d *Metastore) LoadLatest(ctx context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadLatestDynamoDBTimer.UpdateSince(time.Now())

	cond := expression.Key(partitionKey).Equal(expression.Value(keyID))
	proj := expression.NamesList(expression.Name(keyRecord))

	expr, err := expression.NewBuilder().WithKeyCondition(cond).WithProjection(proj).Build()
	if err != nil {
		return nil, fmt.Errorf("dynamodb expression error: %w", err)
	}

	// Have to use query api to use limit and reverse sort order
	res, err := d.svc.Query(ctx, &dynamodb.QueryInput{
		ConsistentRead:            aws.Bool(true), // always use strong consistency
		ExpressionAttributeNames:  expr.Names(),
		ExpressionAttributeValues: expr.Values(),
		KeyConditionExpression:    expr.KeyCondition(),
		Limit:                     aws.Int32(1), // limit 1
		ProjectionExpression:      expr.Projection(),
		ScanIndexForward:          aws.Bool(false), // sorts descending
		TableName:                 aws.String(d.tableName),
	})
	if err != nil {
		return nil, fmt.Errorf("error querying metastore: %w", err)
	}

	if len(res.Items) == 0 {
		return nil, nil
	}

	return decodeItem(res.Items[0])
}

// Store attempts to insert the key into the metastore if one is not already present.
// Returns true if the key was stored, false if it already exists.
// An non-nil error is returned if the operation failed.
func (d *Metastore) Store(ctx context.Context, keyID string, created int64, ekr *appencryption.EnvelopeKeyRecord) (bool, error) {
	defer storeDynamoDBTimer.UpdateSince(time.Now())

	var km *keyMeta
	if ekr.ParentKeyMeta != nil {
		km = &keyMeta{
			ID:      ekr.ParentKeyMeta.ID,
			Created: ekr.ParentKeyMeta.Created,
		}
	}

	en := &envelope{
		Revoked:       ekr.Revoked,
		Created:       ekr.Created,
		EncryptedKey:  base64.StdEncoding.EncodeToString(ekr.EncryptedKey),
		ParentKeyMeta: km,
	}

	av, err := attributevalue.MarshalMap(&en)
	if err != nil {
		return false, fmt.Errorf("failed to marshal envelope: %w", err)
	}

	// Note conditional expression using attribute_not_exists has special semantics. Can be used on partition OR
	// sort key alone to guarantee primary key uniqueness. It automatically checks for existence of this item's
	// composite primary key and if it contains the specified attribute name, either of which is inherently
	// required.
	_, err = d.svc.PutItem(ctx, &dynamodb.PutItemInput{
		Item: map[string]types.AttributeValue{
			partitionKey: &types.AttributeValueMemberS{Value: keyID},
			sortKey:      &types.AttributeValueMemberN{Value: strconv.FormatInt(created, 10)},
			keyRecord:    &types.AttributeValueMemberM{Value: av},
		},
		TableName:           aws.String(d.tableName),
		ConditionExpression: aws.String("attribute_not_exists(" + partitionKey + ")"),
	})
	if err != nil {
		var ccfe *types.ConditionalCheckFailedException
		if errors.As(err, &ccfe) {
			return false, fmt.Errorf("attempted to create duplicate key: %s, %d: %w", keyID, created, err)
		}

		return false, fmt.Errorf("error storing key: %s, %d: %w", keyID, created, err)
	}

	return true, nil
}
