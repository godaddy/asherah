package server

import (
	"database/sql"

	"github.com/go-sql-driver/mysql"
)

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
