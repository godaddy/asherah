package server

import (
	"database/sql"
	"database/sql/driver"
	"os"
	"testing"

	"github.com/stretchr/testify/assert"
)

type FakeDriver struct {
}

func (f *FakeDriver) Open(name string) (driver.Conn, error) {
	return nil, nil
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
