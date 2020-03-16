package main

import (
	"database/sql"
	"log"

	"github.com/go-sql-driver/mysql"
)

var (
	DB *sql.DB
)

// getDB gets a database handle to the mysql instance with the provided connection string.
func getDB(connStr string) error {
	dsn, err := mysql.ParseDSN(connStr)
	if err != nil {
		return err
	}

	dsn.ParseTime = true

	db, err := sql.Open("mysql", dsn.FormatDSN())
	if err != nil {
		return err
	}

	db.SetMaxOpenConns(90)

	DB = db

	return nil
}

func TruncateKeys() {
	txn, err := DB.Prepare("DELETE FROM encryption_key WHERE 1 = 1")
	if err != nil {
		panic(err)
	}

	defer txn.Close()

	result, err := txn.Exec()
	if err != nil {
		panic(err)
	}

	count, err := result.RowsAffected()
	if err != nil {
		panic(err)
	}

	log.Printf("Truncated encryption key table: %d", count)
}
