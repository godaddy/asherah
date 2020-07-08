package persistence_test

import (
	"testing"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

func TestDynamoDBMetastore_WithDynamoDBRegionSuffix(t *testing.T) {
	s := getSession()
	m1 := persistence.NewDynamoDBMetastore(s)

	// keyPrefix should be empty unless WithDynamoDBRegionSuffix is used
	assert.Empty(t, m1.GetRegionSuffix())

	m2 := persistence.NewDynamoDBMetastore(s, persistence.WithDynamoDBRegionSuffix(true))
	// WithDynamoDBRegionSuffix should set the keyPrefix equal to the client's region
	assert.Equal(t, *s.Config.Region, m2.GetRegionSuffix())
}

func getSession() *session.Session {
	sess, err := session.NewSession(&aws.Config{
		Region:   aws.String("us-west-2"),
		Endpoint: aws.String("http://localhost:8000"),
	})

	if err != nil {
		panic(err)
	}

	return sess
}

func TestDynamoDBMetastore_WithTableName(t *testing.T) {
	table := "DummyTable"
	db := persistence.NewDynamoDBMetastore(getSession(), persistence.WithTableName(table))

	assert.Equal(t, table, db.GetTableName())
}

func TestDynamoDBMetastore_DefaultTableName(t *testing.T) {
	defaultTableName := "EncryptionKey"

	db := persistence.NewDynamoDBMetastore(getSession())

	assert.Equal(t, defaultTableName, db.GetTableName())
}
