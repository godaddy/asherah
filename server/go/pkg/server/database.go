package server

import (
	"database/sql"
	"sync"

	"github.com/go-sql-driver/mysql"
)

const (
	ReplicaReadConsistencyQuery         = "SET aurora_replica_read_consistency = ?"
	ReplicaReadConsistencyValueEventual = "eventual"
	ReplicaReadConsistencyValueGlobal   = "global"
	ReplicaReadConsistencyValueSession  = "session"
)

var (
	dbconnection *sql.DB
	dbdriver     = "mysql"
	dbOnce       sync.Once
	dbErr        error
)

func newMysql(connStr string) (*sql.DB, error) {
	dbOnce.Do(func() {
		dsn, err := mysql.ParseDSN(connStr)
		if err != nil {
			dbErr = err
			return
		}

		dsn.ParseTime = true

		dbconnection, dbErr = sql.Open(dbdriver, dsn.FormatDSN())
	})

	return dbconnection, dbErr
}

func setRdbmsReplicaReadConsistencyValue(value string) (err error) {
	if dbconnection != nil {
		switch value {
		case
			ReplicaReadConsistencyValueEventual,
			ReplicaReadConsistencyValueGlobal,
			ReplicaReadConsistencyValueSession:
			_, err = dbconnection.Exec(ReplicaReadConsistencyQuery, value)
		}
	}

	return
}
