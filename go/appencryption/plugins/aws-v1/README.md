# Asherah KMS and Metastore for AWS SDK for Go v1

> The AWS SDK for Go v1 is approaching end-of-life and is now in maintenance mode. We recommend using the AWS SDK for Go v2 along with the [AWS v2 plugins](../aws-v2) for new projects.

This folder contains implementations of Asherah Key Management Service (KMS) and Metastore for AWS SDK for Go v1.

## Packages

The provided implementations are organized into the following packages:

- [`github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms`](./kms): provides an implementation of `appencryption.KeyManagementService`.
- [`github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence`](./persistence): provides an implementation of `appencryption.Metastore`.

## Usage

To use the AWS SDK v1 implementations of Asherah KMS and Metastore, create an instance of each and pass them to the `appencryption.NewSessionFactory` function.

```go
package main

import (
    "context"

    "github.com/godaddy/asherah/go/appencryption"
    "github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
    "github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/kms"
    "github.com/godaddy/asherah/go/appencryption/plugins/aws-v1/persistence"
    "github.com/aws/aws-sdk-go/aws/session"
)

func main() {
    crypto := aead.NewAES256GCM()

    // Create a map of region and ARN pairs that will all be used when creating a System Key
    regionArnMap := map[string]string {
        "us-west-2": "ARN FOR US-WEST-2",
        "us-east-2": "ARN FOR US-EAST-2",
        ...,
    }

    preferredRegion := "us-west-2"

    // Create a KeyManagementService
    keyManagementService, err := kms.NewAWS(crypto, preferredRegion, arnMap)
    if err != nil {
        panic(err)
    }

    sess := session.Must(session.NewSession(
        &aws.Config{
            Region: aws.String("us-west-2"),
        },
    ))

    // Create a Metastore
    metastore, err := persistence.NewDynamoDBMetastore(sess, persistence.WithDynamoDBRegionSuffix(true))
    if err != nil {
        panic(err)
    }

    config := &appencryption.Config{
        Service: "exampleApp",
        Product: "productId",
        Policy:  appencryption.NewCryptoPolicy(),
    }

    // Create a session factory. The builder steps used below are for testing only.
    factory := appencryption.NewSessionFactory(config, metastore, keyManagementService, crypto)

    // At this point, the SessionFactory is ready to be used
    // Example:
    //   session, _ := factory.GetSession("partitionId")
    //   drr, _ := session.Encrypt(context.TODO(), []byte("some data"))
}
```
