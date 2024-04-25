package persistence

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"

	"github.com/godaddy/asherah/go/appencryption"
)

type MemorySuite struct {
	suite.Suite
	ctx             context.Context
	created         int64
	memoryMetastore *MemoryMetastore
	value           appencryption.EnvelopeKeyRecord
}

const (
	keyID          = "ThisIsMyKey"
	value          = "This is my value"
	nonExistentKey = "some non-existent key"
)

func (suite *MemorySuite) SetupSuite() {
	suite.ctx = context.Background()
	suite.created = time.Now().Unix()
}

func (suite *MemorySuite) SetupTest() {
	suite.memoryMetastore = NewMemoryMetastore()

	suite.value.ID = keyID
	suite.value.Created = suite.created
	suite.value.EncryptedKey = []byte(value)
}

func TestNewMemoryMetastore(t *testing.T) {
	metastore := NewMemoryMetastore()

	assert.NotNil(t, metastore.Envelopes)
}

func (suite *MemorySuite) TestMemoryMetastore_StoreAndLoad_ValidKey() {
	if _, err := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value); err != nil {
		suite.T().Logf("error storing in metastore: %s", err)
		panic(err)
	}

	record, err := suite.memoryMetastore.Load(suite.ctx, keyID, suite.created)

	assert.NoError(suite.T(), err)
	assert.Equal(suite.T(), suite.value.ID, record.ID)
	assert.Equal(suite.T(), suite.value.Created, record.Created)
	assert.Equal(suite.T(), suite.value.EncryptedKey, record.EncryptedKey)
}

func (suite *MemorySuite) TestMemoryMetastore_StoreAndLoad_InvalidKey() {
	if _, err := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value); err != nil {
		suite.T().Logf("error storing in metastore: %s", err)
		panic(err)
	}

	record, err := suite.memoryMetastore.Load(suite.ctx, nonExistentKey, suite.created)

	assert.NoError(suite.T(), err)
	assert.Nil(suite.T(), record)
}

func (suite *MemorySuite) TestMemoryMetastore_LoadLatest_MultipleCreatedAndValuesForKeyIdShouldReturnLatest() {
	if _, err := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value); err != nil {
		suite.T().Logf("error storing in metastore: %s", err)
		panic(err)
	}

	createdEpochTime := time.Unix(suite.created, 0)

	createdOneHourLater := createdEpochTime.Add(1 * time.Hour).Unix()
	valueOneHourLater := &appencryption.EnvelopeKeyRecord{
		ID:           keyID,
		Created:      createdOneHourLater,
		EncryptedKey: []byte(fmt.Sprintf("%s%d", value, createdOneHourLater)),
	}

	createdOneDayLater := createdEpochTime.Add(24 * time.Hour).Unix()
	valueOneDayLater := &appencryption.EnvelopeKeyRecord{
		ID:           keyID,
		Created:      createdOneDayLater,
		EncryptedKey: []byte(fmt.Sprintf("%s%d", value, createdOneDayLater)),
	}

	createdOneWeekEarlier := createdEpochTime.Add(-7 * 24 * time.Hour).Unix()
	valueOneWeekEarlier := &appencryption.EnvelopeKeyRecord{
		ID:           keyID,
		Created:      createdOneWeekEarlier,
		EncryptedKey: []byte(fmt.Sprintf("%s%d", value, createdOneWeekEarlier)),
	}

	// intentionally mixing up insertion order
	_, _ = suite.memoryMetastore.Store(suite.ctx, keyID, createdOneHourLater, valueOneHourLater)
	_, _ = suite.memoryMetastore.Store(suite.ctx, keyID, createdOneDayLater, valueOneDayLater)
	_, _ = suite.memoryMetastore.Store(suite.ctx, keyID, createdOneWeekEarlier, valueOneWeekEarlier)

	record, err := suite.memoryMetastore.LoadLatest(suite.ctx, keyID)
	assert.NoError(suite.T(), err)
	assert.Equal(suite.T(), createdOneDayLater, record.Created)
}

func (suite *MemorySuite) TestMemoryMetastore_LoadLatest_NonExistentKeyIdShouldReturnNull() {
	if _, err := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value); err != nil {
		suite.T().Logf("error storing in metastore: %s", err)
		panic(err)
	}

	record, err := suite.memoryMetastore.LoadLatest(suite.ctx, nonExistentKey)
	assert.Nil(suite.T(), record)
	assert.NoError(suite.T(), err)
}

func (suite *MemorySuite) TestMemoryMetastore_Store_WithDuplicateKeyShouldReturnFalse() {
	result, _ := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value)
	assert.True(suite.T(), result)

	result, err := suite.memoryMetastore.Store(suite.ctx, keyID, suite.created, &suite.value)
	assert.NoError(suite.T(), err)
	assert.False(suite.T(), result)
}

func TestMemorySuite(t *testing.T) {
	suite.Run(t, new(MemorySuite))
}
