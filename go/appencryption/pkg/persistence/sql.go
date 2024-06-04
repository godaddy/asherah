package persistence

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"regexp"
	"strconv"
	"time"

	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
)

const (
	defaultLoadKeyQuery    = "SELECT key_record FROM encryption_key WHERE id = ? AND created = ?"
	defaultStoreKeyQuery   = "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)"
	defaultLoadLatestQuery = "SELECT key_record from encryption_key WHERE id = ? ORDER BY created DESC LIMIT 1"
)

var (
	// Verify SQLMetastore implements the Metastore interface.
	_ appencryption.Metastore = (*SQLMetastore)(nil)

	storeSQLTimer      = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.store", appencryption.MetricsPrefix), nil)
	loadSQLTimer       = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.load", appencryption.MetricsPrefix), nil)
	loadLatestSQLTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.loadlatest", appencryption.MetricsPrefix), nil)
)

// SQLMetastoreDBType identifies a specific database/sql driver.
type SQLMetastoreDBType string

const (
	Postgres SQLMetastoreDBType = "postgres"
	Oracle   SQLMetastoreDBType = "oracle"
	MySQL    SQLMetastoreDBType = "mysql"

	DefaultDBType = MySQL
)

var qrx = regexp.MustCompile(`\?`)

// q converts "?" characters to $1, $2, $n on postgres, :1, :2, :n on Oracle.
//
// This function is based on a function of the same name found in the Go
// sql test project: https://github.com/bradfitz/go-sql-test.
func (t SQLMetastoreDBType) q(sql string) string {
	var pref string

	//nolint:exhaustive
	switch t {
	case Postgres:
		pref = "$"
	case Oracle:
		pref = ":"
	default:
		return sql
	}
	n := 0
	return qrx.ReplaceAllStringFunc(sql, func(string) string {
		n++
		return pref + strconv.Itoa(n)
	})
}

// SQLMetastoreOption is used to configure additional options in a SQLMetastore.
type SQLMetastoreOption func(*SQLMetastore)

// WithSQLMetastoreDBType configures the SQLMetastore for use with the specified
// family of database/sql drivers such as Postgres, Oracle, or MySQL (default).
func WithSQLMetastoreDBType(t SQLMetastoreDBType) SQLMetastoreOption {
	return func(s *SQLMetastore) {
		s.dbType = t
		s.loadKeyQuery = t.q(s.loadKeyQuery)
		s.storeKeyQuery = t.q(s.storeKeyQuery)
		s.loadLatestQuery = t.q(s.loadLatestQuery)
	}
}

// SQLMetastore implements the Metastore interface for a RDBMS metastore.
//
// See https://github.com/godaddy/asherah/blob/master/docs/Metastore.md#rdbms for the
// required table structure and other relevant information.
type SQLMetastore struct {
	db *sql.DB

	dbType          SQLMetastoreDBType
	loadKeyQuery    string
	storeKeyQuery   string
	loadLatestQuery string
}

// NewSQLMetastore returns a new SQLMetastore with the provided policy and sql connection.
func NewSQLMetastore(dbHandle *sql.DB, opts ...SQLMetastoreOption) *SQLMetastore {
	metastore := &SQLMetastore{
		db: dbHandle,

		dbType:          DefaultDBType,
		loadKeyQuery:    defaultLoadKeyQuery,
		storeKeyQuery:   defaultStoreKeyQuery,
		loadLatestQuery: defaultLoadLatestQuery,
	}

	for _, opt := range opts {
		opt(metastore)
	}

	return metastore
}

type scanner interface {
	Scan(v ...interface{}) error
}

func parseEnvelope(s scanner) (*appencryption.EnvelopeKeyRecord, error) {
	var keyRecordString string

	if err := s.Scan(&keyRecordString); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, nil
		}

		return nil, errors.Wrap(err, "error from scanner")
	}

	var keyRecord *appencryption.EnvelopeKeyRecord

	if err := json.Unmarshal([]byte(keyRecordString), &keyRecord); err != nil {
		return nil, errors.Wrap(err, "unable to unmarshal key")
	}

	return keyRecord, nil
}

// Load returns the key matching the id and created timestamp provided. The envelope
// will be nil if it does not exist in the metastore.
func (s *SQLMetastore) Load(ctx context.Context, keyID string, created int64) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadSQLTimer.UpdateSince(time.Now())

	t := time.Unix(created, 0)

	return parseEnvelope(s.db.QueryRowContext(ctx, s.loadKeyQuery, keyID, t))
}

// LoadLatest returns the newest record matching the ID.
func (s *SQLMetastore) LoadLatest(ctx context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadLatestSQLTimer.UpdateSince(time.Now())

	return parseEnvelope(s.db.QueryRowContext(ctx, s.loadLatestQuery, keyID))
}

// Store attempts to insert the key into the metastore if one is not
// already present. If a key exists, the method will return false. If
// one is not present, the value will be inserted and we return true.
// Note that as of this writing, the Go sql package doesn't expose a
// way to detect duplicate keys, so they are treated similarly to all
// errors that may happen in the insert.
func (s *SQLMetastore) Store(ctx context.Context, keyID string, created int64, envelope *appencryption.EnvelopeKeyRecord) (bool, error) {
	defer storeSQLTimer.UpdateSince(time.Now())

	bytes, err := json.Marshal(envelope)
	if err != nil {
		return false, errors.Wrap(err, "error marshaling envelope")
	}

	createdAt := time.Unix(created, 0)

	if _, err := s.db.ExecContext(ctx, s.storeKeyQuery, keyID, createdAt, string(bytes)); err != nil {
		// Go sql package does not provide a specific integrity violation error for duplicate detection
		// at this time, so it's treated similar to other errors to avoid error parsing.
		// The caller is left to assume any false/error return value may be a duplicate.
		return false, errors.Wrapf(err, "error storing key: %s, %d", keyID, created)
	}

	return true, nil
}
