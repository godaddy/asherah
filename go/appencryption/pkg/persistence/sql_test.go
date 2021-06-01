package persistence

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"os"
	"strconv"
	"testing"
	"time"

	"github.com/docker/go-connections/nat"
	_ "github.com/go-sql-driver/mysql"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/suite"
	"github.com/testcontainers/testcontainers-go"

	"github.com/godaddy/asherah/go/appencryption"
)

type SQLSuite struct {
	suite.Suite
	disableTestContainers bool
	ctx                   context.Context
	created               int64
	port                  nat.Port
	db                    *sql.DB
	sqlMetastore          *SQLMetastore
	host                  string
	container             testcontainers.Container
}

const (
	maxTriesSQL = 5
	waitTimeSQL = 5

	localHost        = "localhost"
	portProtocolSQL  = "3306/tcp"
	keyWithParent    = "key_with_parent"
	keyWithoutParent = "key_without_parent"
	keyMalformed     = "key_malformed"
	fakeKey          = "fake_key"
	dbName           = "testdb"
	dbUser           = "root"
	dbPass           = "Password123"

	createTableQuery = `CREATE TABLE encryption_key (id VARCHAR(255) NOT NULL,
						created TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
						key_record TEXT NOT NULL,PRIMARY KEY (id, created),INDEX (created))`

	sqlKeyRecordWithParent = `{
	"Revoked":false,
	"ParentKeyMeta": {
		"KeyId":"_SK_api_ecomm",
		"Created":1551980040
	},
	"Key":"WXSRYxyx6YJgv/gCLuYmZo+tCILhPp+Fklx8rZPBH+56zu2hVoI8N8TVDyvi9u+H7akWLD6cYBvAtO5Z",
	"Created":1551980041
}`
	sqlKeyRecordLatest = `{
	"Revoked":false,
	"ParentKeyMeta": {
		"KeyId":"_SK_api_ecomm_latest",
		"Created":1541461380
	},
	"Key":"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
	"Created":1541461380
}`
	sqlKeyRecordWithoutParent = `{
	"Revoked":false,
	"Key":"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr",
	"Created":1541461380
}`
	sqlKeyRecordMalformed = `{
	"Revoked":sqlErrorRecord,
	"ParentKeyMeta": SomeMeta
	}
}`
)

func (suite *SQLSuite) SetupSuite() {
	suite.ctx = context.Background()
	suite.created = time.Now().Add(-24 * time.Hour).Unix()

	// If not using testcontainers, manually set host and port
	suite.disableTestContainers, _ = strconv.ParseBool(os.Getenv("DISABLE_TESTCONTAINERS"))
	if suite.disableTestContainers {
		suite.host = os.Getenv("MYSQL_HOSTNAME")
		if len(suite.host) == 0 {
			suite.host = localHost
		}

		suite.port = portProtocolSQL
	} else {
		request := testcontainers.ContainerRequest{
			Image:        "mysql:5.7",
			ExposedPorts: []string{portProtocolSQL},
			Env: map[string]string{
				"MYSQL_ROOT_PASSWORD": dbPass,
			},
		}
		var err error
		suite.container, err = testcontainers.GenericContainer(suite.ctx, testcontainers.GenericContainerRequest{
			ContainerRequest: request,
			Started:          true,
		})
		if err != nil {
			suite.T().Logf("error creating container: %s", err)
			panic(err)
		}

		// If using testcontainers, get host and port from the container
		if suite.host, err = suite.container.Host(suite.ctx); err != nil {
			suite.T().Logf("error getting host from container: %s", err)
			panic(err)
		}

		if suite.port, err = suite.container.MappedPort(suite.ctx, portProtocolSQL); err != nil {
			suite.T().Logf("error getting mapped port from container: %s", err)
			panic(err)
		}
	}

	db, err := sql.Open("mysql",
		fmt.Sprintf("%s:%s@tcp(%s:%s)/", dbUser, dbPass, suite.host, suite.port.Port()))
	if err != nil {
		suite.T().Logf("error while getting a db object %s", err)
		panic(err)
	}

	suite.db = db
	suite.sqlMetastore = NewSQLMetastore(suite.db)

	waitForDBPing(suite)
}

func waitForDBPing(suite *SQLSuite) {
	err := suite.db.Ping()
	for tries := 1; err != nil; tries++ {
		if tries == maxTriesSQL {
			suite.T().Logf("unable to connect to the MySQL container: %s", err)
			panic(err)
		}

		time.Sleep(waitTimeSQL * time.Second)

		err = suite.db.Ping()
	}
}

func (suite *SQLSuite) TearDownSuite() {
	if !suite.disableTestContainers {
		if err := suite.container.Terminate(suite.ctx); err != nil {
			suite.T().Logf("error terminating container: %s", err)
			panic(err)
		}
	}

	suite.db.Close()
}

func (suite *SQLSuite) SetupTest() {
	suite.executeQuery("CREATE DATABASE IF NOT EXISTS " + dbName)
	suite.executeQuery("USE " + dbName)
	suite.executeQuery(createTableQuery)

	suite.insertIntoDatabase(keyWithParent, time.Unix(suite.created, 0), sqlKeyRecordWithParent)
	suite.insertIntoDatabase(keyWithoutParent, time.Unix(suite.created, 0), sqlKeyRecordWithoutParent)
	suite.insertIntoDatabase(keyMalformed, time.Unix(suite.created, 0), sqlKeyRecordMalformed)
	suite.insertIntoDatabase(keyWithParent, time.Unix(suite.created, 0).Add(10*time.Minute), sqlKeyRecordLatest)
}

func (suite *SQLSuite) TearDownTest() {
	// Blow out the whole db so we have clean slate each time
	suite.executeQuery("DROP DATABASE IF EXISTS " + dbName)
}

func (suite *SQLSuite) executeQuery(query string) {
	if _, err := suite.db.Exec(query); err != nil {
		suite.T().Logf("error while executing query %s. Error: %s", query, err)
		panic(err)
	}
}

func (suite *SQLSuite) insertIntoDatabase(id string, created time.Time, keyRecord string) {
	stmt, err := suite.db.Prepare(storeKeyQuery)
	if err != nil {
		suite.T().Logf("unable to create prepared stmt: %s", err)
		panic(err)
	}

	if _, err := stmt.Exec(id, created, keyRecord); err != nil {
		suite.T().Logf("unable to insert record in the table: %s", err)
		panic(err)
	}
}

func (suite *SQLSuite) TestSQLMetastore_Load_Success() {
	tests := map[string]struct {
		id        string
		keyRecord string
	}{
		"Load envelope with parent key":    {id: keyWithParent, keyRecord: sqlKeyRecordWithParent},
		"Load envelope without parent key": {id: keyWithoutParent, keyRecord: sqlKeyRecordWithoutParent},
	}
	for index := range tests {
		envelope, err := suite.sqlMetastore.Load(suite.ctx, tests[index].id, suite.created)

		assert.NotNil(suite.T(), envelope)
		assert.Nil(suite.T(), err)

		var expectedKeyRecord *appencryption.EnvelopeKeyRecord
		_ = json.Unmarshal([]byte(tests[index].keyRecord), &expectedKeyRecord)

		assert.Equal(suite.T(), expectedKeyRecord, envelope)
	}
}

func (suite *SQLSuite) TestSQLMetastore_Load_WithNoResultShouldReturnNil() {
	envelope, err := suite.sqlMetastore.Load(suite.ctx, fakeKey, suite.created)

	assert.Nil(suite.T(), envelope)
	assert.Nil(suite.T(), err)
}

func (suite *SQLSuite) TestSQLMetastore_Load_WithFailureShouldReturnError() {
	// Explicitly drop the database to force an error
	suite.TearDownTest()
	envelope, err := suite.sqlMetastore.Load(suite.ctx, fakeKey, suite.created)

	assert.Nil(suite.T(), envelope)
	assert.Error(suite.T(), err)
}

func (suite *SQLSuite) TestSQLMetastore_Load_WithInvalidEnvelopeShouldReturnError() {
	envelope, err := suite.sqlMetastore.Load(suite.ctx, keyMalformed, suite.created)

	assert.Nil(suite.T(), envelope)
	assert.Error(suite.T(), err)
}

func (suite *SQLSuite) TestSQLMetastore_LoadLatest_Success() {
	envelope, err := suite.sqlMetastore.LoadLatest(suite.ctx, keyWithParent)

	assert.NotNil(suite.T(), envelope)
	assert.Nil(suite.T(), err)

	var expectedKeyRecord *appencryption.EnvelopeKeyRecord
	_ = json.Unmarshal([]byte(sqlKeyRecordLatest), &expectedKeyRecord)

	assert.Equal(suite.T(), expectedKeyRecord, envelope)
}

func (suite *SQLSuite) TestSQLMetastore_LoadLatest_WithFailureShouldReturnError() {
	// Explicitly drop the database to force an error
	suite.TearDownTest()
	envelope, err := suite.sqlMetastore.LoadLatest(suite.ctx, keyWithParent)

	assert.Nil(suite.T(), envelope)
	assert.Error(suite.T(), err)
}

func (suite *SQLSuite) TestSQLMetastore_Store_Success() {
	res, err := suite.sqlMetastore.Store(suite.ctx, keyWithParent, time.Now().Unix(), nil)

	assert.Nil(suite.T(), err)
	assert.True(suite.T(), res)
}

func (suite *SQLSuite) TestSQLMetastore_Store_WithSqlErrorShouldReturnFalse() {
	// Explicitly drop the database to force an error
	suite.TearDownTest()
	res, err := suite.sqlMetastore.Store(suite.ctx, keyWithParent, time.Now().Unix(), nil)

	assert.Error(suite.T(), err)
	assert.False(suite.T(), res)
}

func (suite *SQLSuite) TestSQLMetastore_Store_WithDuplicateShouldReturnFalse() {
	res, err := suite.sqlMetastore.Store(suite.ctx, keyWithParent, suite.created, nil)

	assert.False(suite.T(), res)
	assert.Error(suite.T(), err)
}

func TestMySqlSuite(t *testing.T) {
	if testing.Short() {
		t.Skip("too slow for testing.Short")
	}

	suite.Run(t, new(SQLSuite))
}
