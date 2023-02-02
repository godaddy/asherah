package persistencetest

import (
	"context"
	"os"
	"strconv"
	"time"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/awserr"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/aws/aws-sdk-go/service/dynamodb"
	"github.com/aws/aws-sdk-go/service/dynamodb/dynamodbattribute"
	"github.com/docker/go-connections/nat"
	"github.com/testcontainers/testcontainers-go"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
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

type DynamoDBTestContext struct {
	instant               int64
	disableTestContainers bool
	sess                  *session.Session
	container             testcontainers.Container
	dbSvc                 *dynamodb.DynamoDB
	dynamodbMetastore     *persistence.DynamoDBMetastore
}

func (d *DynamoDBTestContext) GetMetastore() *persistence.DynamoDBMetastore {
	if d.dynamodbMetastore == nil {
		d.dynamodbMetastore = d.NewMetastore()
	}

	return d.dynamodbMetastore
}

func (d *DynamoDBTestContext) NewMetastore(opts ...persistence.DynamoDBMetastoreOption) *persistence.DynamoDBMetastore {
	combinedOpts := []persistence.DynamoDBMetastoreOption{
		persistence.WithTableName(tableName),
	}

	if len(opts) > 0 {
		combinedOpts = append(combinedOpts, opts...)
	}

	return persistence.NewDynamoDBMetastore(d.sess, combinedOpts...)
}

func (d *DynamoDBTestContext) SeedDB() {
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
	if _, err := d.dbSvc.CreateTable(input); err != nil {
		panic(err)
	}

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
	d.putItemInDynamoDB(d.getDynamoDBItem(en, d.instant))
}

func (d *DynamoDBTestContext) CleanDB() {
	// Blow out the whole table so we have clean slate each time
	deleteTableInput := &dynamodb.DeleteTableInput{
		TableName: aws.String(tableName),
	}

	if _, err := d.dbSvc.DeleteTable(deleteTableInput); err != nil {
		// We may have already deleted the table in some test cases
		if e, ok := err.(awserr.Error); ok && e.Code() == dynamodb.ErrCodeResourceNotFoundException {
			return
		}

		panic(err)
	}
}

func (d *DynamoDBTestContext) InsertTestItem(instant int64) {
	d.putItemInDynamoDB(d.getDynamoDBItem(persistence.DynamoDBEnvelope{Created: instant}, instant))
}

func (d *DynamoDBTestContext) putItemInDynamoDB(item *dynamodb.PutItemInput) {
	if _, err := d.dbSvc.PutItem(item); err != nil {
		panic(err)
	}
}

func (d *DynamoDBTestContext) getDynamoDBItem(envelope persistence.DynamoDBEnvelope, instant int64) *dynamodb.PutItemInput {
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

func (d *DynamoDBTestContext) waitForDynamoDBPing() {
	// Check if dynamodb is up and running
	listTableInput := new(dynamodb.ListTablesInput)
	_, err := d.dbSvc.ListTables(listTableInput)

	for tries := 1; err != nil; tries++ {
		if tries == maxTriesDynamoDB {
			// suite.T().Logf("unable to connect to the DynamoDB container: %s", err)
			panic(err)
		}

		time.Sleep(waitTimeDynamoDB * time.Second)

		_, err = d.dbSvc.ListTables(listTableInput)
	}
}

func (d *DynamoDBTestContext) TearDown() {
	// Don't call terminate if we are not using test containers
	if !d.disableTestContainers {
		if err := d.container.Terminate(context.Background()); err != nil {
			panic(err)
		}
	}
}

func NewDynamoDBTestContext(instant int64) *DynamoDBTestContext {
	ctx := context.Background()
	d := &DynamoDBTestContext{
		instant: instant,
	}

	// Setup client pointing to our local dynamodb
	var (
		err             error
		host            string
		dynamodbNatPort nat.Port
	)

	d.disableTestContainers, err = strconv.ParseBool(os.Getenv("DISABLE_TESTCONTAINERS"))
	if err != nil {
		d.disableTestContainers = false
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

	d.sess, err = session.NewSession(&aws.Config{
		Region:   aws.String("us-west-2"),
		Endpoint: aws.String("http://" + host + ":" + dynamodbNatPort.Port()),
	})
	if err != nil {
		panic(err)
	}

	d.dbSvc = dynamodb.New(d.sess)

	d.waitForDynamoDBPing()

	return d
}
