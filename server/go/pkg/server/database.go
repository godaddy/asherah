package server

import (
	"database/sql"

	"github.com/go-sql-driver/mysql"
)

var (
	dbconnection *sql.DB
	dbdriver     = "mysql"
)

func newMysql(connStr string) (*sql.DB, error) {
	if dbconnection == nil {
		dsn, err := mysql.ParseDSN(connStr)
		if err != nil {
			return nil, err
		}

		dsn.ParseTime = true

		dbconnection, err = sql.Open(dbdriver, dsn.FormatDSN())
		if err != nil {
			return nil, err
		}
	}

	return dbconnection, nil
}
