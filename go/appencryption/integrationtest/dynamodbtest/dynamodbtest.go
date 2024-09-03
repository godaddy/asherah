// Package dynamodbtest provides utilities for testing the DynamoDB persistence layer.
package dynamodbtest

import (
	"context"
	"os"
	"strconv"
	"testing"
	"time"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/awserr"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/aws/aws-sdk-go/service/dynamodb"
	"github.com/aws/aws-sdk-go/service/dynamodb/dynamodbattribute"
	"github.com/docker/go-connections/nat"
	"github.com/testcontainers/testcontainers-go"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence"
)

const (
	IKCreated          = int64(1541461381)
	SKCreated          = int64(1541461380)
	EncryptedKeyString = "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr"
	SKKeyID            = "_SK_api_ecomm"
	TestKey            = "some_key"
)

const (
	tableName            = "CustomTableName"
	portProtocolDynamoDB = "8000/tcp"
	maxTriesDynamoDB     = 5
	waitTimeDynamoDB     = 10
	partitionKey         = "Id"
	sortKey              = "Created"
	keyRecord            = "KeyRecord"
)

type BaseDynamoDBTestContext struct {
	instant               int64
	disableTestContainers bool
	container             testcontainers.Container

	endpoint string
}

func NewBaseContext(t *testing.T, instant int64) *BaseDynamoDBTestContext {
	t.Helper()

	ctx := context.Background()
	d := &BaseDynamoDBTestContext{
		instant: instant,
	}

	// Setup client pointing to our local dynamodb
	var (
		err             error
		host            string
		dynamodbNatPort nat.Port
	)

	if val, ok := os.LookupEnv("DISABLE_TESTCONTAINERS"); ok {
		d.disableTestContainers, err = strconv.ParseBool(val)
		if err != nil {
			panic(err)
		}
	}

	if d.disableTestContainers {
		host = os.Getenv("DYNAMODB_HOSTNAME")
		if len(host) == 0 {
			host = "localhost"
		}

		dynamodbNatPort = portProtocolDynamoDB
	} else {
		request := testcontainers.ContainerRequest{
			Image:        "amazon/dynamodb-local:latest",
			ExposedPorts: []string{portProtocolDynamoDB},
		}
		d.container, err = testcontainers.GenericContainer(ctx, testcontainers.GenericContainerRequest{
			ContainerRequest: request,
			Started:          true,
		})
		if err != nil {
			panic(err)
		}

		if host, err = d.container.Host(ctx); err != nil {
			panic(err)
		}

		if dynamodbNatPort, err = d.container.MappedPort(ctx, portProtocolDynamoDB); err != nil {
			panic(err)
		}
	}

	d.endpoint = "http://" + host + ":" + dynamodbNatPort.Port()

	return d
}

// Endpoint returns the endpoint for the DynamoDB instance.
func (d *BaseDynamoDBTestContext) Endpoint() string {
	return d.endpoint
}

type DynamoDBTestContext struct {
	BaseDynamoDBTestContext

	sess  *session.Session
	dbSvc *dynamodb.DynamoDB
}

// NewDynamoDBTestContext creates a new DynamoDBTestContext.
//
// If the DISABLE_TESTCONTAINERS environment variable is set to true, the test context will use the local DynamoDB instance.
// Otherwise, it will use a test container.
//
// Use NewMetastore to create a new DynamoDBMetastore instance based on the context.
//
// (Optionally) Use SeedDB to create the expected DynamoDB table and seed it with test data.
// It may be skipped if the table already exists, or if the table is not needed for the test.
//
// Use CleanDB to clean the DynamoDB table as needed.
func NewDynamoDBTestContext(t *testing.T, instant int64) *DynamoDBTestContext {
	d := &DynamoDBTestContext{
		BaseDynamoDBTestContext: *NewBaseContext(t, instant),
	}

	t.Logf("using dynamodb endpoint: %s", d.Endpoint())

	d.sess = session.Must(session.NewSession(&aws.Config{
		Region:   aws.String("us-west-2"),
		Endpoint: aws.String(d.Endpoint()),
	}))

	d.dbSvc = dynamodb.New(d.sess)

	d.waitForDynamoDBPing(t)

	return d
}

// NewMetastore creates a new DynamoDBMetastore instance based on the context.
func (d *DynamoDBTestContext) NewMetastore(opts ...persistence.DynamoDBMetastoreOption) *persistence.DynamoDBMetastore {
	combinedOpts := []persistence.DynamoDBMetastoreOption{
		persistence.WithTableName(tableName),
	}

	if len(opts) > 0 {
		combinedOpts = append(combinedOpts, opts...)
	}

	return persistence.NewDynamoDBMetastore(d.sess, combinedOpts...)
}

// SeedDB creates the DynamoDB table and seeds it with test data.
func (d *DynamoDBTestContext) SeedDB(t *testing.T) {
	t.Helper()
	t.Log("seeding dynamodb")

	// Create table schema
	input := &dynamodb.CreateTableInput{
		AttributeDefinitions: []*dynamodb.AttributeDefinition{
			{
				AttributeName: aws.String(partitionKey),
				AttributeType: aws.String("S"),
			},
			{
				AttributeName: aws.String(sortKey),
				AttributeType: aws.String("N"),
			},
		},
		KeySchema: []*dynamodb.KeySchemaElement{
			{
				AttributeName: aws.String(partitionKey),
				KeyType:       aws.String("HASH"),
			},
			{
				AttributeName: aws.String(sortKey),
				KeyType:       aws.String("RANGE"),
			},
		},
		ProvisionedThroughput: &dynamodb.ProvisionedThroughput{
			ReadCapacityUnits:  aws.Int64(1),
			WriteCapacityUnits: aws.Int64(1),
		},
		TableName: aws.String(tableName),
	}

	o, err := d.dbSvc.CreateTable(input)
	if err != nil {
		panic(err)
	}

	t.Log("table created:", o.TableDescription.TableArn, "@", o.TableDescription.CreationDateTime)

	describeTableInput := &dynamodb.DescribeTableInput{TableName: aws.String(tableName)}
	if err := d.dbSvc.WaitUntilTableExists(describeTableInput); err != nil {
		panic(err)
	}

	t.Log("finished waiting for table")

	// Add item to table
	km := appencryption.KeyMeta{
		ID:      SKKeyID,
		Created: SKCreated,
	}
	en := persistence.DynamoDBEnvelope{
		Revoked:       false,
		Created:       IKCreated,
		EncryptedKey:  EncryptedKeyString,
		ParentKeyMeta: &km,
	}
	d.putItemInDynamoDB(t, d.newDynamoDBPutItemInput(en, d.instant))
}

// CleanDB "cleans up" the DynamoDB table by deleting it.
//
// This is useful for resetting table state in between test runs.
// Be sure to call SeedDB before running any additional tests that require the table to be populated.
func (d *DynamoDBTestContext) CleanDB(t *testing.T) {
	t.Helper()
	t.Log("cleaning db")

	// Blow out the whole table so we have clean slate each time
	deleteTableInput := &dynamodb.DeleteTableInput{
		TableName: aws.String(tableName),
	}

	if _, err := d.dbSvc.DeleteTable(deleteTableInput); err != nil {
		// We may have already deleted the table in some test cases
		if e, ok := err.(awserr.Error); ok && e.Code() == dynamodb.ErrCodeResourceNotFoundException {
			t.Logf("ignoring error on delete table: %v", e)

			return
		}

		panic(err)
	}

	t.Log("waiting for table delete")

	describeTableInput := &dynamodb.DescribeTableInput{TableName: aws.String(tableName)}
	if err := d.dbSvc.WaitUntilTableNotExists(describeTableInput); err != nil {
		panic(err)
	}

	t.Log("finished waiting for table delete")
}

func (d *DynamoDBTestContext) InsertTestItem(t *testing.T, instant int64) {
	t.Helper()

	d.putItemInDynamoDB(t, d.newDynamoDBPutItemInput(persistence.DynamoDBEnvelope{Created: instant}, instant))
}

func (d *DynamoDBTestContext) putItemInDynamoDB(t *testing.T, item *dynamodb.PutItemInput) {
	t.Helper()
	t.Log("putting item")

	_, err := d.dbSvc.PutItem(item)
	if err != nil {
		panic(err)
	}
}

func (d *DynamoDBTestContext) newDynamoDBPutItemInput(envelope persistence.DynamoDBEnvelope, instant int64) *dynamodb.PutItemInput {
	// Get item to add in table
	av, err := dynamodbattribute.MarshalMap(&envelope)
	if err != nil {
		panic(err)
	}

	input := &dynamodb.PutItemInput{
		Item: map[string]*dynamodb.AttributeValue{
			partitionKey: {S: aws.String(TestKey)},
			sortKey:      {N: aws.String(strconv.FormatInt(instant, 10))},
			keyRecord:    {M: av},
		},
		TableName: aws.String(tableName),
	}

	return input
}

func (d *DynamoDBTestContext) waitForDynamoDBPing(t *testing.T) {
	t.Helper()

	// Check if dynamodb is up and running
	listTableInput := new(dynamodb.ListTablesInput)
	_, err := d.dbSvc.ListTables(listTableInput)

	for tries := 1; err != nil; tries++ {
		t.Logf("failed to list dynamodb tables (tried %d of max %d)", tries, maxTriesDynamoDB)

		if tries == maxTriesDynamoDB {
			panic(err)
		}

		time.Sleep(waitTimeDynamoDB * time.Second)

		_, err = d.dbSvc.ListTables(listTableInput)
	}

	t.Log("finished waiting for dynamodb")
}

func (d *DynamoDBTestContext) TearDown(t *testing.T) {
	t.Helper()
	t.Log("tearing down")

	// Don't call terminate if we are not using test containers
	if !d.disableTestContainers {
		t.Log("terminating test container")

		if err := d.container.Terminate(context.Background()); err != nil {
			panic(err)
		}
	}
}
