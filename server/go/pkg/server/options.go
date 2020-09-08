package server

import (
	"errors"
	"strings"
	"time"
)

//nolint:lll,staticcheck
type Options struct {
	ServiceName          string        `long:"service" required:"yes" description:"The name of this service" env:"ASHERAH_SERVICE_NAME"`
	ProductID            string        `long:"product" required:"yes" description:"The name of the product that owns this service" env:"ASHERAH_PRODUCT_NAME"`
	ExpireAfter          time.Duration `long:"expire-after" description:"The amount of time a key is considered valid" env:"ASHERAH_EXPIRE_AFTER"`
	CheckInterval        time.Duration `long:"check-interval" description:"The amount of time before cached keys are considered stale" env:"ASHERAH_CHECK_INTERVAL"`
	Metastore            string        `long:"metastore" choice:"rdbms" choice:"dynamodb" choice:"memory" required:"yes" description:"Determines the type of metastore to use for persisting keys" env:"ASHERAH_METASTORE_MODE"`
	ConnectionString     string        `long:"conn" default-mask:"-" description:"The database connection string (required if --metastore=rdbms)" env:"ASHERAH_CONNECTION_STRING"`
	DynamoDBEndpoint     string        `long:"dynamodb-endpoint" description:"An optional endpoint URL (hostname only or fully qualified URI) (only supported by --metastore=dynamodb)" env:"ASHERAH_DYNAMODB_ENDPOINT"`
	DynamoDBRegion       string        `long:"dynamodb-region" description:"The AWS region for DynamoDB requests (defaults to globally configured region) (only supported by --metastore=dynamodb)" env:"ASHERAH_DYNAMODB_REGION"`
	DynamoDBTableName    string        `long:"dynamodb-table-name" description:"The table name for DynamoDB (only supported by --metastore=dynamodb)" env:"ASHERAH_DYNAMODB_TABLE_NAME"`
	EnableRegionSuffix   bool          `long:"enable-region-suffix" description:"Configure the metastore to use regional suffixes (only supported by --metastore=dynamodb)" env:"ASHERAH_ENABLE_REGION_SUFFIX"`
	EnableSessionCaching bool          `long:"enable-session-caching" description:"Enable shared session caching" env:"ASHERAH_ENABLE_SESSION_CACHING"`
	SessionCacheMaxSize  int           `long:"session-cache-max-size" default:"1000" description:"Define the maximum number of sessions to cache" env:"ASHERAH_SESSION_CACHE_MAX_SIZE"`
	SessionCacheDuration time.Duration `long:"session-cache-duration" default:"2h" description:"The amount of time a session will remain cached" env:"ASHERAH_SESSION_CACHE_DURATION"`
	KMS                  string        `long:"kms" choice:"aws" choice:"static" default:"aws" description:"Configures the master key management service" env:"ASHERAH_KMS_MODE"`
	RegionMap            RegionMap     `long:"region-map" description:"A comma separated list of key-value pairs in the form of REGION1=ARN1[,REGION2=ARN2] (required if --kms=aws)" env:"ASHERAH_REGION_MAP"`
	PreferredRegion      string        `long:"preferred-region" description:"The preferred AWS region (required if --kms=aws)" env:"ASHERAH_PREFERRED_REGION"`
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
