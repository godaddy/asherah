package persistence

import (
	"context"
	"encoding/base64"
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
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"
	"github.com/testcontainers/testcontainers-go"

	"github.com/godaddy/asherah/go/appencryption"
)

type DynamoDBSuite struct {
	suite.Suite
	disableTestContainers     bool
	ctx                       context.Context
	dbSvc                     *dynamodb.DynamoDB
	instant                   int64
	sess                      *session.Session
	container                 testcontainers.Container
	dynamodbMetastore         *DynamoDBMetastore
	prefixedDynamodbMetastore *DynamoDBMetastore
}

const (
	tableName            = "CustomTableName"
	portProtocolDynamoDB = "8000/tcp"
	maxTriesDynamoDB     = 5
	waitTimeDynamoDB     = 10
	ikCreated            = int64(1541461381)
	skCreated            = int64(1541461380)
	encryptedKeyString   = "mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr"
	skKeyID              = "_SK_api_ecomm"
	testKey              = "some_key"
)

func (suite *DynamoDBSuite) SetupSuite() {
	suite.ctx = context.Background()
	suite.instant = time.Now().Add(-24 * time.Hour).Unix()

	// Setup client pointing to our local dynamodb
	var (
		err             error
		host            string
		dynamodbNatPort nat.Port
	)

	suite.disableTestContainers, _ = strconv.ParseBool(os.Getenv("DISABLE_TESTCONTAINERS"))
	if suite.disableTestContainers {
		host = "localhost"
		dynamodbNatPort = nat.Port(portProtocolDynamoDB)
	} else {
		request := testcontainers.ContainerRequest{
			Image:        "amazon/dynamodb-local:latest",
			ExposedPorts: []string{portProtocolDynamoDB},
		}
		suite.container, err = testcontainers.GenericContainer(suite.ctx, testcontainers.GenericContainerRequest{
			ContainerRequest: request,
			Started:          true,
		})
		if err != nil {
			suite.T().Logf("error creating container: %s", err)
			panic(err)
		}

		if host, err = suite.container.Host(suite.ctx); err != nil {
			suite.T().Logf("error getting host ip: %s", err)
			panic(err)
		}

		if dynamodbNatPort, err = suite.container.MappedPort(suite.ctx, nat.Port(portProtocolDynamoDB)); err != nil {
			suite.T().Logf("error getting mapped dynamodbPort: %s", err)
			panic(err)
		}
	}

	suite.sess, err = session.NewSession(&aws.Config{
		Region:   aws.String("us-west-2"),
		Endpoint: aws.String("http://" + host + ":" + dynamodbNatPort.Port()),
	})
	if err != nil {
		suite.T().Logf("error creating new dynamodb session: %s", err)
		panic(err)
	}

	suite.dbSvc = dynamodb.New(suite.sess)

	waitForDynamoDBPing(suite)
}

func waitForDynamoDBPing(suite *DynamoDBSuite) {
	// Check if dynamodb is up and running
	listTableInput := new(dynamodb.ListTablesInput)
	_, err := suite.dbSvc.ListTables(listTableInput)

	for tries := 1; err != nil; tries++ {
		if tries == maxTriesDynamoDB {
			suite.T().Logf("unable to connect to the DynamoDB container: %s", err)
			panic(err)
		}

		time.Sleep(waitTimeDynamoDB * time.Second)

		_, err = suite.dbSvc.ListTables(listTableInput)
	}
}

func (suite *DynamoDBSuite) TearDownSuite() {
	// Don't call terminate if we are not using test containers
	if !suite.disableTestContainers {
		if err := suite.container.Terminate(suite.ctx); err != nil {
			suite.T().Logf("error terminating container: %s", err)
			panic(err)
		}
	}
}

func (suite *DynamoDBSuite) SetupTest() {
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
	if _, err := suite.dbSvc.CreateTable(input); err != nil {
		suite.T().Logf("error calling CreateTable: %s", err)
		panic(err)
	}

	// Add item to table
	km := appencryption.KeyMeta{
		ID:      skKeyID,
		Created: skCreated,
	}
	en := dynamoDBEnvelope{
		Revoked:       false,
		Created:       ikCreated,
		EncryptedKey:  encryptedKeyString,
		ParentKeyMeta: &km,
	}
	suite.putItemInDynamoDB(getDynamoDBItem(en, suite.instant))

	suite.dynamodbMetastore = NewDynamoDBMetastore(suite.sess, WithTableName(tableName))
	suite.prefixedDynamodbMetastore = NewDynamoDBMetastore(suite.sess, WithDynamoDBRegionSuffix(true))
}

func (suite *DynamoDBSuite) TearDownTest() {
	// Blow out the whole table so we have clean slate each time
	deleteTableInput := &dynamodb.DeleteTableInput{
		TableName: aws.String(tableName),
	}

	if _, err := suite.dbSvc.DeleteTable(deleteTableInput); err != nil {
		// We may have already deleted the table in some test cases
		if e, ok := err.(awserr.Error); ok && e.Code() == dynamodb.ErrCodeResourceNotFoundException {
			return
		}

		suite.T().Logf("error calling delete table: %s", err)
		panic(err)
	}
}

func getDynamoDBItem(envelope dynamoDBEnvelope, instant int64) *dynamodb.PutItemInput {
	// Get item to add in table
	av, err := dynamodbattribute.MarshalMap(&envelope)
	if err != nil {
		panic(err)
	}

	input := &dynamodb.PutItemInput{
		Item: map[string]*dynamodb.AttributeValue{
			partitionKey: {S: aws.String(testKey)},
			sortKey:      {N: aws.String(strconv.FormatInt(instant, 10))},
			keyRecord:    {M: av},
		},
		TableName: aws.String(tableName),
	}

	return input
}

func (suite *DynamoDBSuite) putItemInDynamoDB(item *dynamodb.PutItemInput) {
	if _, err := suite.dbSvc.PutItem(item); err != nil {
		suite.T().Logf("error put item in local dynamo: %s", err)
	}
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_Success() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.dynamodbMetastore.Load(suite.ctx, testKey, suite.instant)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), ikCreated, envelope.Created)
	assert.Equal(suite.T(), bytes, envelope.EncryptedKey)
	assert.Equal(suite.T(), skCreated, envelope.ParentKeyMeta.Created)
	assert.Equal(suite.T(), skKeyID, envelope.ParentKeyMeta.ID)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.dynamodbMetastore.Load(suite.ctx, "fake_key", suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.TearDownTest()
	envelope, _ := suite.dynamodbMetastore.Load(suite.ctx, testKey, suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithSingleRecord() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.dynamodbMetastore.LoadLatest(suite.ctx, testKey)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), ikCreated, envelope.Created)
	assert.Equal(suite.T(), bytes, envelope.EncryptedKey)
	assert.Equal(suite.T(), skCreated, envelope.ParentKeyMeta.Created)
	assert.Equal(suite.T(), skKeyID, envelope.ParentKeyMeta.ID)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithMultipleRecords() {
	currentEpochTime := time.Unix(suite.instant, 0)
	timeMinusOneHour := currentEpochTime.Add(-1 * time.Hour).Unix()
	timePlusOneHour := currentEpochTime.Add(1 * time.Hour).Unix()
	timeMinusOneDay := currentEpochTime.Add(-24 * time.Hour).Unix()
	timePlusOneDay := currentEpochTime.Add(24 * time.Hour).Unix()

	// intentionally mixing up insertion order
	suite.putItemInDynamoDB(getDynamoDBItem(dynamoDBEnvelope{Created: timePlusOneHour}, timePlusOneHour))
	suite.putItemInDynamoDB(getDynamoDBItem(dynamoDBEnvelope{Created: timePlusOneDay}, timePlusOneDay))
	suite.putItemInDynamoDB(getDynamoDBItem(dynamoDBEnvelope{Created: timeMinusOneHour}, timeMinusOneHour))
	suite.putItemInDynamoDB(getDynamoDBItem(dynamoDBEnvelope{Created: timeMinusOneDay}, timeMinusOneDay))

	envelope, _ := suite.dynamodbMetastore.LoadLatest(suite.ctx, testKey)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), timePlusOneDay, envelope.Created)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.dynamodbMetastore.LoadLatest(suite.ctx, "fake_key")

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.TearDownTest()
	envelope, _ := suite.dynamodbMetastore.LoadLatest(suite.ctx, testKey)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Store_Success() {
	km := appencryption.KeyMeta{
		ID:      skKeyID,
		Created: skCreated,
	}
	en := &appencryption.EnvelopeKeyRecord{
		Created:       ikCreated,
		ParentKeyMeta: &km,
	}
	res, _ := suite.dynamodbMetastore.Store(suite.ctx, testKey, time.Now().Unix(), en)

	assert.True(suite.T(), res)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Store_WithDuplicateShouldReturnFalse() {
	km := appencryption.KeyMeta{
		ID:      skKeyID,
		Created: skCreated,
	}
	en := &appencryption.EnvelopeKeyRecord{
		Created:       ikCreated,
		ParentKeyMeta: &km,
	}
	insertTime := time.Now().Unix()
	firstAttempt, _ := suite.dynamodbMetastore.Store(suite.ctx, testKey, insertTime, en)
	secondAttempt, _ := suite.dynamodbMetastore.Store(suite.ctx, testKey, insertTime, en)

	assert.True(suite.T(), firstAttempt)
	assert.False(suite.T(), secondAttempt)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Store_WithFailureShouldReturnError() {
	km := appencryption.KeyMeta{
		ID:      skKeyID,
		Created: skCreated,
	}
	en := &appencryption.EnvelopeKeyRecord{
		Created:       ikCreated,
		ParentKeyMeta: &km,
	}
	// Explicitly delete the table to force an error
	suite.TearDownTest()
	res, err := suite.dynamodbMetastore.Store(suite.ctx, testKey, time.Now().Unix(), en)

	assert.False(suite.T(), res)
	assert.NotNil(suite.T(), err)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_WithDynamoDBRegionSuffix() {
	// keyPrefix should be empty unless WithDynamoDBRegionSuffix is used
	assert.Empty(suite.T(), suite.dynamodbMetastore.GetKeySuffix())

	// WithDynamoDBRegionSuffix should set the keyPrefix equal to the client's region
	assert.Equal(suite.T(), *suite.sess.Config.Region, suite.prefixedDynamodbMetastore.GetKeySuffix())
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_WithTableName() {
	table := "DummyTable"
	db := NewDynamoDBMetastore(suite.sess, WithTableName(table))

	assert.Equal(suite.T(), table, db.tableName)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_DefaultTableName() {
	db := NewDynamoDBMetastore(suite.sess)

	assert.Equal(suite.T(), defaultTableName, db.tableName)
}

func TestDynamoSuite(t *testing.T) {
	suite.Run(t, new(DynamoDBSuite))
}
