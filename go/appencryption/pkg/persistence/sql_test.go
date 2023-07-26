package persistence

import (
	"database/sql"
	"testing"

	"github.com/stretchr/testify/suite"
)

// SQLSuite provides unit tests for the sql metastore implementation.
//
// Note that the integration tests formerly provided by this package have been relocated to
// a new dedicated module: github.com/godaddy/asherah/go/appencryption/integrationtest.
type SQLSuite struct {
	suite.Suite
}

func TestMySqlSuite(t *testing.T) {
	suite.Run(t, new(SQLSuite))
}

func (s *SQLSuite) TestNewSQLMetastore() {
	db := &sql.DB{}

	m := NewSQLMetastore(db)

	s.Equal(MySQL, m.dbType)
	s.Equal(defaultLoadKeyQuery, m.loadKeyQuery)
	s.Equal(defaultStoreKeyQuery, m.storeKeyQuery)
	s.Equal(defaultLoadLatestQuery, m.loadLatestQuery)
}

func (s *SQLSuite) TestNewSQLMetastore_WithSQLMetastoreDBType() {
	tests := []struct {
		dbType                  SQLMetastoreDBType
		expectedLoadKeyQuery    string
		expectedStoreKeyQuery   string
		expectedLoadLatestQuery string
	}{
		{
			dbType:                  Postgres,
			expectedLoadKeyQuery:    "SELECT key_record FROM encryption_key WHERE id = $1 AND created = $2",
			expectedStoreKeyQuery:   "INSERT INTO encryption_key (id, created, key_record) VALUES ($1, $2, $3)",
			expectedLoadLatestQuery: "SELECT key_record from encryption_key WHERE id = $1 ORDER BY created DESC LIMIT 1",
		},
		{
			dbType:                  Oracle,
			expectedLoadKeyQuery:    "SELECT key_record FROM encryption_key WHERE id = :1 AND created = :2",
			expectedStoreKeyQuery:   "INSERT INTO encryption_key (id, created, key_record) VALUES (:1, :2, :3)",
			expectedLoadLatestQuery: "SELECT key_record from encryption_key WHERE id = :1 ORDER BY created DESC LIMIT 1",
		},
		{
			dbType:                  MySQL,
			expectedLoadKeyQuery:    "SELECT key_record FROM encryption_key WHERE id = ? AND created = ?",
			expectedStoreKeyQuery:   "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)",
			expectedLoadLatestQuery: "SELECT key_record from encryption_key WHERE id = ? ORDER BY created DESC LIMIT 1",
		},
	}

	db := &sql.DB{}

	for i := range tests {
		tt := tests[i]
		s.Run(string(tt.dbType), func() {
			m := NewSQLMetastore(db, WithSQLMetastoreDBType(tt.dbType))

			s.Equal(tt.dbType, m.dbType)
			s.Equal(tt.expectedLoadKeyQuery, m.loadKeyQuery)
			s.Equal(tt.expectedStoreKeyQuery, m.storeKeyQuery)
			s.Equal(tt.expectedLoadLatestQuery, m.loadLatestQuery)
		})
	}
}
