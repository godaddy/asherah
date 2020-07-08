package server

import (
	"database/sql"

	"github.com/go-sql-driver/mysql"
)

var (
	dbconnection *sql.DB
)

func getMysql(connStr string, pool bool) (*sql.DB, error) {
	if !pool {
		return newMysql(connStr)
	}

	if (*sql.DB)(nil) == dbconnection {
		var err error
		dbconnection, err = newMysql(connStr)
		
		if err != nil {
			return nil, err
		}
	}

	return dbconnection, nil
}

func newMysql(connStr string) (*sql.DB, error) {
	dsn, err := mysql.ParseDSN(connStr)
	if err != nil {
		return nil, err
	}

	dsn.ParseTime = true

	db, err := sql.Open("mysql", dsn.FormatDSN())
	if err != nil {
		return nil, err
	}

	return db, nil
}
