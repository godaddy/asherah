package metastore_test

import (
	"context"
	"fmt"

	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb"

	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
)

func Example_withCustomConfig() {
	// Load the AWS SDK's default configuration from the environment and override the region
	cfg, err := config.LoadDefaultConfig(context.Background(), config.WithRegion("us-west-2"))
	if err != nil {
		panic(fmt.Sprintf("unable to load SDK config, %v", err))
	}

	// Create an AWS DynamoDB client with the custom configuration
	client := dynamodb.NewFromConfig(cfg)

	// Create a new DynamoDB Metastore with the custom client and enable region suffix
	store, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client), metastore.WithRegionSuffix(true))
	if err != nil {
		panic(fmt.Sprintf("unable to create Metastore, %v", err))
	}

	// At this point, the Metastore is ready to be used
	// Example:
	//   factory := appencryption.NewSessionFactory(config, store, kms, crypto)
	//   session, err := factory.GetSession("partitionId")
	//   ...
	//
	// But for this example, just print the Metastore's region suffix
	fmt.Println(store.GetRegionSuffix())

	// Output:
	// us-west-2
}
