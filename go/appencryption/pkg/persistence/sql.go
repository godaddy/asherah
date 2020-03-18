package persistence

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"time"

	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
)

const (
	loadKeyQuery    = "SELECT key_record FROM encryption_key WHERE id = ? AND created = ?"
	storeKeyQuery   = "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)"
	loadLatestQuery = "SELECT key_record from encryption_key WHERE id = ? ORDER BY created DESC LIMIT 1"
)

var (
	// Verify SQLMetastore implements the Metastore interface.
	_ appencryption.Metastore = (*SQLMetastore)(nil)

	storeSQLTimer      = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.store", appencryption.MetricsPrefix), nil)
	loadSQLTimer       = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.load", appencryption.MetricsPrefix), nil)
	loadLatestSQLTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.metastore.sql.loadlatest", appencryption.MetricsPrefix), nil)
)

// SQLMetastore implements the Metastore interface for a RDBMS metastore.
// See scripts/encryption_key.sql for table structure and required
// stored procedures.
type SQLMetastore struct {
	db *sql.DB
}

// NewSQLMetastore returns a new SQLMetastore with the provided policy and sql connection.
func NewSQLMetastore(dbHandle *sql.DB) *SQLMetastore {
	return &SQLMetastore{
		db: dbHandle,
	}
}

type scanner interface {
	Scan(v ...interface{}) error
}

func parseEnvelope(s scanner) (*appencryption.EnvelopeKeyRecord, error) {
	var keyRecordString string

	if err := s.Scan(&keyRecordString); err != nil {
		if err == sql.ErrNoRows {
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

	return parseEnvelope(s.db.QueryRowContext(ctx, loadKeyQuery, keyID, t))
}

// LoadLatest returns the newest record matching the ID.
func (s *SQLMetastore) LoadLatest(ctx context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	defer loadLatestSQLTimer.UpdateSince(time.Now())

	return parseEnvelope(s.db.QueryRowContext(ctx, loadLatestQuery, keyID))
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

	if _, err := s.db.ExecContext(ctx, storeKeyQuery, keyID, createdAt, string(bytes)); err != nil {
		// Go sql package does not provide a specific integrity violation error for duplicate detection
		// at this time, so it's treated similar to other errors to avoid error parsing.
		// The caller is left to assume any false/error return value may be a duplicate.
		return false, errors.Wrapf(err, "error storing key: %s, %d", keyID, created)
	}

	return true, nil
}
