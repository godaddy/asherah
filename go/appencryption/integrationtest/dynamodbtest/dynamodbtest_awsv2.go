package dynamodbtest

import (
	"context"
	"fmt"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
)

// NewMetastore creates a new plugins/aws-v2/dynamodb/metastore.Metastore instance based on the context.
func (d *DynamoDBTestContext) NewMetastoreV2(useRegionSuffix bool) *metastore.Metastore {
	cfg, err := config.LoadDefaultConfig(context.Background(), config.WithRegion("us-west-2"))
	if err != nil {
		panic(fmt.Sprintf("unable to load SDK config, %v", err))
	}

	client := dynamodb.NewFromConfig(cfg, func(o *dynamodb.Options) {
		o.BaseEndpoint = aws.String(d.Endpoint())
	})

	m, err := metastore.NewDynamoDB(
		metastore.WithDynamoDBClient(client),
		metastore.WithRegionSuffix(useRegionSuffix),
		metastore.WithTableName(tableName),
	)
	if err != nil {
		panic(fmt.Sprintf("unable to create Metastore, %v", err))
	}

	return m
}
