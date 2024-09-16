# Asherah KMS and Metastore for AWS SDK for Go v2

This folder contains implementations of Asherah Key Management Service (KMS) and Metastore for AWS SDK for Go v2.

## Packages

The provided implementations are organized into the following packages:

- [`github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms`](./kms): provides an implementation of `appencryption.KeyManagementService`.
- [`github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore`](./dynamodb/metastore): provides an implementation of `appencryption.Metastore`.


## Usage

To use the AWS SDK v2 implementations of Asherah KMS and Metastore, create an instance of each and pass them to the `appencryption.NewSessionFactory` function.

```go
package main

import (
    "context"
    "fmt"

    "github.com/aws/aws-sdk-go-v2/config"
    "github.com/aws/aws-sdk-go-v2/service/dynamodb"

    "github.com/godaddy/asherah/go/appencryption"
    "github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
    "github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
    "github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms"
)

func main() {
    // Load the default AWS SDK configuration with the desired region
    awsCfg, err := config.LoadDefaultConfig(context.TODO(),
        config.WithRegion("us-west-2"),
    )
    if err != nil {
        fmt.Println("unable to load SDK config, ", err)
        return
    }

    crypto := aead.NewAES256GCM()

    // Create a map of region and ARN pairs that will all be used when creating a System Key
    arnMap := map[string]string{
        "us-west-2": "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab",
    }

    // Create a new Asherah KMS using the custom configuration for the underlying AWS KMS client
    keyManagementService, err := kms.NewBuilder(crypto, arnMap).
        WithAWSConfig(awsCfg).
        Build()
    if err != nil {
        fmt.Println("unable to create AWS KMS, ", err)
        return
    }

    // Create a new DynamoDB client with the custom configuration
    client := dynamodb.NewFromConfig(awsCfg)

    // Create a new DynamoDB Metastore with the custom client
    store, err := metastore.NewDynamoDB(metastore.WithDynamoDBClient(client))
    if err != nil {
        fmt.Println("unable to create Metastore, ", err)
        return
    }

    asherahCfg := &appencryption.Config{
        Service: "some-service",
        Product: "some-product",
        Policy:  appencryption.NewCryptoPolicy(),
    }

    // Create a new SessionFactory with the custom Metastore and KMS
    factory := appencryption.NewSessionFactory(asherahCfg, store, keyManagementService, crypto)
    defer factory.Close()

    // At this point, the SessionFactory is ready to be used
    // Example:
    //   session, _ := factory.GetSession("partitionId")
    //   drr, _ := session.Encrypt(context.TODO(), []byte("some data"))
}
```
