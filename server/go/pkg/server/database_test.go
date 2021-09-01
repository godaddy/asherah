package server

import (
	"context"
	"database/sql"
	"database/sql/driver"
	"fmt"
	"os"
	"testing"

	"github.com/stretchr/testify/assert"
)

var (
	readConsistencyValue string
)

type FakeResult struct {}

func (r *FakeResult) LastInsertId() (int64, error) {
	return 0, nil
}

func (r *FakeResult) RowsAffected() (int64, error) {
	return 0, nil
}

type FakeConn struct {
}

func (c *FakeConn) Begin() (driver.Tx, error) {
	return nil, nil
}

func (c *FakeConn) Close() error {
	return nil
}

func (c *FakeConn) ExecContext(ctx context.Context, query string, args []driver.NamedValue) (driver.Result, error) {
	result := &FakeResult{}
	switch query {
	case ReplicaReadConsistencyQuery:
		readConsistencyValue = fmt.Sprintf("%v", args[0].Value)
	}
	return result, nil
}

func (c *FakeConn) Prepare(query string) (driver.Stmt, error) {
	return nil, nil
}

type FakeDriver struct {
}

func (f *FakeDriver) Open(name string) (driver.Conn, error) {
	conn := &FakeConn{}
	return conn, nil
}

func TestMain(m *testing.M) {
	dbdriver = "fake"

	sql.Register("fake", &FakeDriver{})

	os.Exit(m.Run())
}

func TestNewMysql(t *testing.T) {
	assert.Nil(t, dbconnection)

	_, err := newMysql("root:secret@(localhost:%s)/mysql")

	assert.Nil(t, err)
	assert.NotNil(t, dbconnection)
}

func TestSetRdbmsReplicaReadConsistencyValue(t *testing.T) {
	dbconnection = nil

	var err error
	db, err := newMysql("root:secret@(localhost:%s)/mysql")
	assert.Nil(t, err)
	assert.NotNil(t, db)

	// empty
	err = setRdbmsReplicaReadConsistencyValue("")
	assert.Nil(t, err)
	assert.Equal(t, "", readConsistencyValue)
	// eventual
	err = setRdbmsReplicaReadConsistencyValue(ReplicaReadConsistencyValueEventual)
	assert.Nil(t, err)
	assert.Equal(t, ReplicaReadConsistencyValueEventual, readConsistencyValue)
	// global
	err = setRdbmsReplicaReadConsistencyValue(ReplicaReadConsistencyValueGlobal)
	assert.Nil(t, err)
	assert.Equal(t, ReplicaReadConsistencyValueGlobal, readConsistencyValue)
	// session
	err = setRdbmsReplicaReadConsistencyValue(ReplicaReadConsistencyValueSession)
	assert.Nil(t, err)
	assert.Equal(t, ReplicaReadConsistencyValueSession, readConsistencyValue)
}
