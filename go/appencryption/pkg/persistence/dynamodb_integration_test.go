package persistence_test

import (
	"context"
	"encoding/base64"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence/persistencetest"
)

type DynamoDBSuite struct {
	suite.Suite
	instant           int64
	dynamodbMetastore *persistence.DynamoDBMetastore
	testContext       *persistencetest.DynamoDBTestContext
}

const (
	ikCreated          = persistencetest.IKCreated
	skCreated          = persistencetest.SKCreated
	encryptedKeyString = persistencetest.EncryptedKeyString
	skKeyID            = persistencetest.SKKeyID
	testKey            = persistencetest.TestKey
)

func (suite *DynamoDBSuite) SetupSuite() {
	suite.instant = time.Now().Add(-24 * time.Hour).Unix()
	suite.testContext = persistencetest.NewDynamoDBTestContext(suite.instant)
}

func (suite *DynamoDBSuite) TearDownSuite() {
	suite.testContext.TearDown()
}

func (suite *DynamoDBSuite) SetupTest() {
	suite.testContext.SeedDB()
	suite.dynamodbMetastore = suite.testContext.GetMetastore()
}

func (suite *DynamoDBSuite) TearDownTest() {
	suite.testContext.CleanDB()
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_Success() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.dynamodbMetastore.Load(context.Background(), testKey, suite.instant)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), ikCreated, envelope.Created)
	assert.Equal(suite.T(), bytes, envelope.EncryptedKey)
	assert.Equal(suite.T(), skCreated, envelope.ParentKeyMeta.Created)
	assert.Equal(suite.T(), skKeyID, envelope.ParentKeyMeta.ID)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.dynamodbMetastore.Load(context.Background(), "fake_key", suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_Load_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.TearDownTest()
	envelope, _ := suite.dynamodbMetastore.Load(context.Background(), testKey, suite.instant)

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithSingleRecord() {
	bytes, _ := base64.StdEncoding.DecodeString(encryptedKeyString)

	envelope, _ := suite.dynamodbMetastore.LoadLatest(context.Background(), testKey)

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
	suite.testContext.InsertTestItem(timePlusOneHour)
	suite.testContext.InsertTestItem(timePlusOneDay)
	suite.testContext.InsertTestItem(timeMinusOneHour)
	suite.testContext.InsertTestItem(timeMinusOneDay)

	envelope, _ := suite.dynamodbMetastore.LoadLatest(context.Background(), persistencetest.TestKey)

	assert.NotNil(suite.T(), envelope)
	assert.Equal(suite.T(), timePlusOneDay, envelope.Created)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithNoResultShouldReturnEmpty() {
	envelope, _ := suite.dynamodbMetastore.LoadLatest(context.Background(), "fake_key")

	assert.Nil(suite.T(), envelope)
}

func (suite *DynamoDBSuite) TestDynamoDBMetastore_LoadLatest_WithFailureShouldReturnEmpty() {
	// Explicitly delete the table to force an error
	suite.TearDownTest()
	envelope, _ := suite.dynamodbMetastore.LoadLatest(context.Background(), testKey)

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
	res, _ := suite.dynamodbMetastore.Store(context.Background(), testKey, time.Now().Unix(), en)

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
	firstAttempt, _ := suite.dynamodbMetastore.Store(ctx, testKey, insertTime, en)
	secondAttempt, _ := suite.dynamodbMetastore.Store(ctx, testKey, insertTime, en)

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
	res, err := suite.dynamodbMetastore.Store(context.Background(), testKey, time.Now().Unix(), en)

	assert.False(suite.T(), res)
	assert.NotNil(suite.T(), err)
}

func TestDynamoSuite(t *testing.T) {
	if testing.Short() {
		t.Skip("too slow for testing.Short")
	}

	suite.Run(t, new(DynamoDBSuite))
}
