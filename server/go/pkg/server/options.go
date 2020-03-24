package server

import (
	"errors"
	"strings"
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
	RegionMap        RegionMap     `long:"region-map" description:"A comma separated list of key-value pairs in the form of REGION1=ARN1[,REGION2=ARN2] (required if --kms=aws)"`
	PreferredRegion  string        `long:"preferred-region" description:"The preferred AWS region (required if --kms=aws)"`
}

type RegionMap map[string]string

func (r RegionMap) UnmarshalFlag(value string) error {
	pairs := strings.Split(value, ",")
	for _, pair := range pairs {
		parts := strings.Split(pair, "=")
		if len(parts) != 2 || len(parts[1]) == 0 {
			return errors.New("argument must be in the form of REGION1=ARN1[,REGION2=ARN2]")
		}
		region, arn := parts[0], parts[1]
		r[region] = arn
	}

	return nil
}
