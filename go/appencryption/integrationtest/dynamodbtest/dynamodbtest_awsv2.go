package dynamodbtest

import (
	"context"
	"errors"
	"fmt"
	"strconv"
	"testing"
	"time"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/feature/dynamodb/attributevalue"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb/types"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
)

// DynamoDBTestContextV2 provides AWS SDK v2 test utilities for DynamoDB integration testing.
type DynamoDBTestContextV2 struct {
	BaseDynamoDBTestContext

	cfg    aws.Config
	client *dynamodb.Client
}

// testEnvelope represents the envelope key record for AWS SDK v2 tests.
// This mirrors the structure used by the AWS v2 metastore plugin without importing v1 dependencies.
type testEnvelope struct {
	Revoked       bool             `dynamodbav:"Revoked,omitempty"`
	Created       int64            `dynamodbav:"Created"`
	EncryptedKey  string           `dynamodbav:"Key"`
	ParentKeyMeta *testKeyMeta     `dynamodbav:"ParentKeyMeta,omitempty"`
}

// testKeyMeta contains the ID and Created timestamp for an encryption key.
type testKeyMeta struct {
	ID      string `dynamodbav:"KeyId"`
	Created int64  `dynamodbav:"Created"`
}

// NewDynamoDBTestContextV2 creates a new DynamoDBTestContextV2 with AWS SDK v2.
//
// If the DISABLE_TESTCONTAINERS environment variable is set to true, the test context will use the local DynamoDB instance.
// Otherwise, it will use a test container.
//
// Use NewMetastoreV2 to create a new DynamoDB metastore instance based on the context.
//
// (Optionally) Use SeedDBV2 to create the expected DynamoDB table and seed it with test data.
// It may be skipped if the table already exists, or if the table is not needed for the test.
//
// Use CleanDBV2 to clean the DynamoDB table as needed.
func NewDynamoDBTestContextV2(t *testing.T, instant int64) *DynamoDBTestContextV2 {
	d := &DynamoDBTestContextV2{
		BaseDynamoDBTestContext: *NewBaseContext(t, instant),
	}

	t.Logf("using dynamodb endpoint: %s", d.Endpoint())

	var err error
	d.cfg, err = config.LoadDefaultConfig(context.Background(), config.WithRegion("us-west-2"))
	if err != nil {
		panic(fmt.Sprintf("unable to load SDK config, %v", err))
	}

	d.client = dynamodb.NewFromConfig(d.cfg, func(o *dynamodb.Options) {
		o.BaseEndpoint = aws.String(d.Endpoint())
	})

	d.waitForDynamoDBPingV2(t)

	return d
}

// NewMetastoreV2 creates a new plugins/aws-v2/dynamodb/metastore.Metastore instance based on the context.
func (d *DynamoDBTestContextV2) NewMetastoreV2(useRegionSuffix bool) *metastore.Metastore {
	m, err := metastore.NewDynamoDB(
		metastore.WithDynamoDBClient(d.client),
		metastore.WithRegionSuffix(useRegionSuffix),
		metastore.WithTableName(tableName),
	)
	if err != nil {
		panic(fmt.Sprintf("unable to create Metastore, %v", err))
	}

	return m
}

// SeedDBV2 creates the DynamoDB table and seeds it with test data using AWS SDK v2.
func (d *DynamoDBTestContextV2) SeedDBV2(t *testing.T) {
	t.Helper()
	t.Log("seeding dynamodb with AWS SDK v2")

	// Create table schema
	input := &dynamodb.CreateTableInput{
		AttributeDefinitions: []types.AttributeDefinition{
			{
				AttributeName: aws.String(partitionKey),
				AttributeType: types.ScalarAttributeTypeS,
			},
			{
				AttributeName: aws.String(sortKey),
				AttributeType: types.ScalarAttributeTypeN,
			},
		},
		KeySchema: []types.KeySchemaElement{
			{
				AttributeName: aws.String(partitionKey),
				KeyType:       types.KeyTypeHash,
			},
			{
				AttributeName: aws.String(sortKey),
				KeyType:       types.KeyTypeRange,
			},
		},
		ProvisionedThroughput: &types.ProvisionedThroughput{
			ReadCapacityUnits:  aws.Int64(1),
			WriteCapacityUnits: aws.Int64(1),
		},
		TableName: aws.String(tableName),
	}

	result, err := d.client.CreateTable(context.Background(), input)
	if err != nil {
		panic(err)
	}

	t.Log("table created:", result.TableDescription.TableArn, "@", result.TableDescription.CreationDateTime)

	// Wait for table to become active
	waiter := dynamodb.NewTableExistsWaiter(d.client)
	err = waiter.Wait(context.Background(), &dynamodb.DescribeTableInput{
		TableName: aws.String(tableName),
	}, 2*time.Minute)
	if err != nil {
		panic(err)
	}

	t.Log("finished waiting for table")

	// Add item to table
	km := &testKeyMeta{
		ID:      SKKeyID,
		Created: SKCreated,
	}
	en := testEnvelope{
		Revoked:       false,
		Created:       IKCreated,
		EncryptedKey:  EncryptedKeyString,
		ParentKeyMeta: km,
	}
	d.putItemInDynamoDBV2(t, d.newDynamoDBPutItemInputV2(en, d.instant))
}

// CleanDBV2 "cleans up" the DynamoDB table by deleting it using AWS SDK v2.
//
// This is useful for resetting table state in between test runs.
// Be sure to call SeedDBV2 before running any additional tests that require the table to be populated.
func (d *DynamoDBTestContextV2) CleanDBV2(t *testing.T) {
	t.Helper()
	t.Log("cleaning db with AWS SDK v2")

	// Blow out the whole table so we have clean slate each time
	deleteTableInput := &dynamodb.DeleteTableInput{
		TableName: aws.String(tableName),
	}

	_, err := d.client.DeleteTable(context.Background(), deleteTableInput)
	if err != nil {
		// We may have already deleted the table in some test cases
		var rnfe *types.ResourceNotFoundException
		if errors.As(err, &rnfe) {
			t.Logf("ignoring error on delete table: %v", rnfe)
			return
		}
		panic(err)
	}

	t.Log("waiting for table delete")

	waiter := dynamodb.NewTableNotExistsWaiter(d.client)
	err = waiter.Wait(context.Background(), &dynamodb.DescribeTableInput{
		TableName: aws.String(tableName),
	}, 2*time.Minute)
	if err != nil {
		panic(err)
	}

	t.Log("finished waiting for table delete")
}

// InsertTestItemV2 inserts a test item using AWS SDK v2.
func (d *DynamoDBTestContextV2) InsertTestItemV2(t *testing.T, instant int64) {
	t.Helper()

	d.putItemInDynamoDBV2(t, d.newDynamoDBPutItemInputV2(testEnvelope{Created: instant}, instant))
}

func (d *DynamoDBTestContextV2) putItemInDynamoDBV2(t *testing.T, item *dynamodb.PutItemInput) {
	t.Helper()
	t.Log("putting item with AWS SDK v2")

	_, err := d.client.PutItem(context.Background(), item)
	if err != nil {
		panic(err)
	}
}

func (d *DynamoDBTestContextV2) newDynamoDBPutItemInputV2(envelope testEnvelope, instant int64) *dynamodb.PutItemInput {
	// Get item to add in table
	av, err := attributevalue.MarshalMap(&envelope)
	if err != nil {
		panic(err)
	}

	input := &dynamodb.PutItemInput{
		Item: map[string]types.AttributeValue{
			partitionKey: &types.AttributeValueMemberS{Value: TestKey},
			sortKey:      &types.AttributeValueMemberN{Value: strconv.FormatInt(instant, 10)},
			keyRecord:    &types.AttributeValueMemberM{Value: av},
		},
		TableName: aws.String(tableName),
	}

	return input
}

func (d *DynamoDBTestContextV2) waitForDynamoDBPingV2(t *testing.T) {
	t.Helper()

	// Check if dynamodb is up and running
	listTableInput := &dynamodb.ListTablesInput{}
	_, err := d.client.ListTables(context.Background(), listTableInput)

	for tries := 1; err != nil; tries++ {
		t.Logf("failed to list dynamodb tables (tried %d of max %d)", tries, maxTriesDynamoDB)

		if tries == maxTriesDynamoDB {
			panic(err)
		}

		time.Sleep(waitTimeDynamoDB * time.Second)

		_, err = d.client.ListTables(context.Background(), listTableInput)
	}

	t.Log("finished waiting for dynamodb")
}

// TearDownV2 performs cleanup for AWS SDK v2 test context.
func (d *DynamoDBTestContextV2) TearDownV2(t *testing.T) {
	t.Helper()
	t.Log("tearing down AWS SDK v2 test context")

	// Don't call terminate if we are not using test containers
	if !d.disableTestContainers {
		t.Log("terminating test container")

		if err := d.container.Terminate(context.Background()); err != nil {
			panic(err)
		}
	}
}
