package cltf

import (
	"fmt"
	"os"
)

const (
	KeyManagementStaticMasterKey = "mysupersecretstaticmasterkey!!!!"

	DefaultServiceID   = "service"
	DefaultProductID   = "product"
	DefaultPartitionID = "partition"

	FileDirectory = "/tmp/"
	FileName      = "go_encrypted"
)

var (
	MysqlDatbaseName = os.Getenv("TEST_DB_NAME")
	MysqlUsername    = os.Getenv("TEST_DB_USER")
	MysqlPassword    = os.Getenv("TEST_DB_PASSWORD")

	ConnectionString = fmt.Sprintf("%s:%s@tcp(localhost:3306)/%s", MysqlUsername, MysqlPassword, MysqlDatbaseName)
)
