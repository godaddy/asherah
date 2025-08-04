package persistence_test

import (
	"context"
	"encoding/base64"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/integrationtest/dynamodbtest"
)

const (
	ikCreated          = dynamodbtest.IKCreated
	skCreated          = dynamodbtest.SKCreated
	encryptedKeyString = dynamodbtest.EncryptedKeyString
	skKeyID            = dynamodbtest.SKKeyID
	testKey            = dynamodbtest.TestKey
)

// DynamoDBSuite is the test suite for the DynamoDBMetastore.
// Tests are run against a local DynamoDB instance using testcontainers.
type DynamoDBSuite struct {
	suite.Suite

	instant           int64
	dynamodbMetastore appencryption.Metastore
	testContext       *dynamodbtest.DynamoDBTestContext
}

// Metastore is the SUT for the test suite.
func (suite *DynamoDBSuite) Metastore() appencryption.Metastore {
	return suite.dynamodbMetastore
}

func (suite *DynamoDBSuite) SetupSuite() {
	suite.instant = time.Now().Add(-24 * time.Hour).Unix()
	suite.testContext = dynamodbtest.NewDynamoDBTestContext(suite.T(), suite.instant)
}

func (suite *DynamoDBSuite) TearDownSuite() {
	suite.testContext.TearDown(suite.T())
}

func (suite *DynamoDBSuite) SetupTest() {
	suite.testContext.SeedDB(suite.T())
	suite.dynamodbMetastore = suite.testContext.NewMetastore()
}

func (suite *DynamoDBSuite) TearDownTest() {
	suite.testContext.CleanDB(suite.T())
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_Success() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.Metastore().Load(context.Background(), testKey, suite.instant)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), ikCreated, envelope.Created)
	assert.Equal(suite.T(), bytes, envelope.EncryptedKey)
	assert.Equal(suite.T(), skCreated, envelope.ParentKeyMeta.Created)
	assert.Equal(suite.T(), skKeyID, envelope.ParentKeyMeta.ID)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.Metastore().Load(context.Background(), "fake_key", suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.testContext.CleanDB(suite.T())

	envelope, _ := suite.Metastore().Load(context.Background(), testKey, suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithSingleRecord() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.Metastore().LoadLatest(context.Background(), testKey)

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
	suite.testContext.InsertTestItem(suite.T(), timePlusOneHour)
	suite.testContext.InsertTestItem(suite.T(), timePlusOneDay)
	suite.testContext.InsertTestItem(suite.T(), timeMinusOneHour)
	suite.testContext.InsertTestItem(suite.T(), timeMinusOneDay)

	envelope, _ := suite.Metastore().LoadLatest(context.Background(), dynamodbtest.TestKey)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), timePlusOneDay, envelope.Created)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.Metastore().LoadLatest(context.Background(), "fake_key")

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.testContext.CleanDB(suite.T())

	envelope, _ := suite.Metastore().LoadLatest(context.Background(), testKey)

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
	res, _ := suite.Metastore().Store(context.Background(), testKey, time.Now().Unix(), en)

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
	ctx := context.Background()
	firstAttempt, _ := suite.Metastore().Store(ctx, testKey, insertTime, en)
	secondAttempt, _ := suite.Metastore().Store(ctx, testKey, insertTime, en)

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
	suite.testContext.CleanDB(suite.T())

	res, err := suite.Metastore().Store(context.Background(), testKey, time.Now().Unix(), en)

	assert.False(suite.T(), res)
	assert.NotNil(suite.T(), err)
}

func TestDynamoDBSuite(t *testing.T) {
	if testing.Short() {
		t.Skip("too slow for testing.Short")
	}

	suite.Run(t, new(DynamoDBSuite))
}
