package server

import (
	"time"
)

// nolint:staticcheck multiple choice tags are supported
type Options struct {
	ServiceName      string        `long:"service" required:"yes" description:"The name of this service"`
	ProductId        string        `long:"product" required:"yes" description:"The name of the product that owns this service"`
	ExpireAfter      time.Duration `long:"expire-after" description:"The amount of time a key is considered valid"`
	CheckInterval    time.Duration `long:"check-interval" description:"The amount of time before cached keys are considered stale"`
	Metastore        string        `long:"metastore" choice:"rdbms" choice:"dynamodb" required:"yes" description:"Determines the type of metastore to use for persisting keys"`
	ConnectionString string        `long:"conn" description:"The database connection string (required if --metastore=rdbms)"`
	KMS              string        `long:"kms" choice:"aws" choice:"static" default:"aws" description:"Configures the master key management service"`
}
