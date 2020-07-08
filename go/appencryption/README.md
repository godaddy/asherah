# Asherah - Go
Application level envelope encryption SDK for Golang with support for cloud-agnostic data storage and key management.

[![GoDoc](https://godoc.org/github.com/godaddy/asherah/go/appencryption?status.svg)](https://pkg.go.dev/github.com/godaddy/asherah/go/appencryption)

  * [Quick Start](#quick-start)
  * [How to Use Asherah](#how-to-use-asherah)
    * [Define the Metastore](#define-the-metastore)
    * [Define the Key Management Service](#define-the-key-management-service)
    * [Define the Crypto Policy](#define-the-crypto-policy)
    * [(Optional) Enable Metrics](#optional-enable-metrics)
    * [Build a Session Factory](#build-a-session-factory)
    * [Performing Cryptographic Operations](#performing-cryptographic-operations)
  * [Documentation](#documentation)
  * [Development Notes](#development-notes)

## Quick Start

```go
package main

import (
    "github.com/godaddy/asherah/go/appencryption"
    "github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
    "github.com/godaddy/asherah/go/appencryption/pkg/kms"
    "github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

func main() {
    crypto := aead.NewAES256GCM()
    config := &appencryption.Config{
        Service: "reference_app",
        Product: "productId",
        Policy:  appencryption.NewCryptoPolicy(),
    }
    metastore := persistence.NewMemoryMetastore()
    key, err := kms.NewStatic("thisIsAStaticMasterKeyForTesting", crypto)
    if err != nil {
        panic(err)
    }

    // Create a session factory. The builder steps used below are for testing only.
    factory := appencryption.NewSessionFactory(config, metastore, key, crypto)
    defer factory.Close()

    // Now create a cryptographic session for a partition.
    sess, err := factory.GetSession("shopper123")
    if err != nil {
        panic(err)
    }
    // Close frees the memory held by the intermediate keys used in this session
    defer sess.Close()

    // Now encrypt some data
    dataRow, err := sess.Encrypt([]byte("mysupersecretpayload"))
    if err != nil {
        panic(err)
    }

    //Decrypt the data
    data, err := sess.Decrypt(*dataRow)
    if err != nil {
        panic(err)
    }
}
```

A more extensive example is the [Reference Application](../../samples/go/referenceapp/), which will evolve along 
with the SDK.

## How to Use Asherah

Before you can start encrypting data, you need to define Asherah's required pluggable components. Below we show how to
build the various options for each component.

### Define the Metastore

Detailed information about the Metastore, including any provisioning steps, can be found [here](../../docs/Metastore.md).

#### RDBMS Metastore

Asherah can connect to a relational database by accepting a connection string and opening a connection to the database
using a database driver name. See https://golang.org/s/sqldrivers for a list of third-party drivers.

```go
// Open a DB connection using a database driver name and connection string
connectionString := ...;

// Parse the DSN string to a Config
dsn, err := mysql.ParseDSN(connectionString)
if err != nil {
    return err
}

// Open a connection to the database using the driver name and the connection string
db, err := sql.Open("driver-name", dsn.FormatDSN())
if err != nil {
    return err
}

// Build the Metastore
metastore := persistence.NewSQLMetastore(db)
```

#### DynamoDB Metastore

```go
awsConfig := &aws.Config{
    Region: aws.String("us-west-2"), // specify preferred region here
}

sess, err = session.NewSession(awsConfig)
if err != nil {
    panic(err)
}

// To configure an endpoint
awsConfig.Endpoint = aws.String("http://localhost:8000"), 
```
You can also either use the `WithXXX` functional options to configure the metastore properties.

 - **WithDynamoDBRegionSuffix**: Specifies whether regional suffixes should be enabled for DynamoDB. Enabling this
  suffixes the keys with the DynamoDb preferred region. **This is required to enable Global Tables**.
 - **WithTableName**: Specifies the name of the DynamoDb table.

``` go
// Build the Metastore
metastore := persistence.NewDynamoDBMetastore(
    sess,
    persistence.WithDynamoDBRegionSuffix(true),
    persistence.WithTableName("CustomTableName") ,
)
```

#### In-memory Metastore (FOR TESTING ONLY)

```go
metastore := persistence.NewMemoryMetastore()
```

### Define the Key Management Service
Detailed information about the Key Management Service can be found [here](../../docs/KeyManagementService.md).

#### AWS KMS

```go
// Create a map of region and ARN pairs that will all be used when creating a System Key
regionArnMap := map[string]string {
    "us-west-2": "ARN FOR US-WEST-2",
    "us-east-2": "ARN FOR US-EAST-2",
    "eu-west-2": "ARN FOR EU-WEST-2",
    ...,
}
crypto := aead.NewAES256GCM()

// Build the Key Management Service using the region dictionary and your preferred (usually current) region
keyManagementService :=  kms.NewAWS(crypto, "us-west-2", regionArnMap)
```

#### Static KMS (FOR TESTING ONLY)

```go
crypto := aead.NewAES256GCM()
keyManagementService := kms.NewStatic("thisIsAStaticMasterKeyForTesting", crypto)
```

### Define the Crypto Policy
Detailed information on Crypto Policy can be found [here](../../docs/CryptoPolicy.md). The Crypto Policy's effect 
on key caching is explained [here](../../docs/KeyCaching.md).

#### Basic Expiring Crypto Policy

```go
cryptoPolicy := appencryption.NewCryptoPolicy()
```

The default key expiration limit is 90 days and revoke check interval is 60 minutes. These can be changed using 
functional options.

```go
cryptoPolicy := appencryption.NewCryptoPolicy(
    appencryption.WithExpireAfterDuration(24 * time.Hour),
    appencryption.WithRevokeCheckInterval(30 * time.Minute))
```

### (Optional) Enable Metrics
Asherah's Go implementation uses [go-metrics](https://github.com/rcrowley/go-metrics) for metrics, which are enabled by 
default. If metrics are to be disabled, we simply use the `WithMetrics` functional option while creating the 
`SessionFactory`.

```go
factory := NewSessionFactory(config, metastore, kms, crypto, WithMetrics(false))
```

The following metrics are available:
- *ael.drr.decrypt:* Total time spent on all operations that were needed to decrypt.
- *ael.drr.encrypt:* Total time spent on all operations that were needed to encrypt.
- *ael.kms.aws.decrypt.\<region\>:* Time spent on decrypting the region-specific keys.
- *ael.kms.aws.decryptkey:* Total time spend in decrypting the key which would include the region-specific decrypt calls
in case of transient failures.
- *ael.kms.aws.encrypt.\<region\>:* Time spent on data key plain text encryption for each region.
- *ael.kms.aws.encryptkey:* Total time spent in encrypting the key which would include the region-specific 
generatedDataKey and parallel encrypt calls.
- *ael.kms.aws.generatedatakey.\<region\>:* Time spent to generate the first data key which is then encrypted in 
remaining regions.
- *ael.metastore.sql.load:* Time spent to load a record from sql metastore.
- *ael.metastore.sql.loadlatest:* Time spent to get the latest record from sql metastore.
- *ael.metastore.sql.store:* Time spent to store a record into sql metastore.
- *ael.metastore.dynamodb.load:* Time spent to load a record from DynamoDB metastore.
- *ael.metastore.dynamodb.loadlatest:* Time spent to get the latest record from DynamoDB metastore.
- *ael.metastore.dynamodb.store:* Time spent to store a record into DynamoDB metastore.

### Build a Session Factory

A session factory can now be built using the components we defined above.

```go
sessionFactory := appencryption.NewSessionFactory(
    &appencryption.Config{
        Service: "reference_app",
        Product: "productId",
        Policy:  appencryption.NewCryptoPolicy(),
    },
    metastore,
    kms,
    crypto,
)
```

**NOTE:** We recommend that every service have its own session factory, preferably as a singleton instance within the 
service. This will allow you to leverage caching and minimize resource usage. Always remember to close the session 
factory before exiting the service to ensure that all resources held by the factory, including the cache, are disposed 
of properly.

### Performing Cryptographic Operations

Create a session to be used for cryptographic operations.

```go
sess, err := factory.GetSession("shopper123")
if err != nil {
    panic(err)
}
// Close frees the memory held by the intermediate keys used in this session
defer sess.Close() 
```

**NOTE:** Remember to close the session after all cryptographic operations to dispose of associated resources.

#### Encrypt/Decrypt
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is 
completely up to the calling application for storage responsibility.

```go
originalPayloadString := "mysupersecretpayload";

// encrypt the payload
dataRowRecord, err := sess.Encrypt([]byte(originalPayloadString))
if err != nil {
    panic(err)
}

// decrypt the payload
decryptedPayload, err := sess.Decrypt(*dataRowRecord)
if err != nil {
    panic(err)
}
```

## Documentation

**appencryption package:** See the [godocs](https://godoc.org/github.com/godaddy/asherah/go/appencryption) for api documentation.

## Development Notes

### Unit Tests

Some unit tests will use the AWS SDK, If you don’t already have a local
[AWS credentials file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html),
create a *dummy* file called **`~/.aws/credentials`** with the below contents:

```
[default]
aws_access_key_id = foobar
aws_secret_access_key = barfoo
```

Alternately, you can set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

