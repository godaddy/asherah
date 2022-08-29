# Asherah Server - Go

Table of Contents
=================

  * [Running the server](#running-the-server)
  * [Configuring the server](#configuring-the-server)

## Running the server
The following makes use of the `rdbms` metastore implementation and assumes mysql is running on localhost and a
preexisting asherah database. See [metastore documentation](/docs/Metastore.md) for more.

```console
[user@machine go]$ go build -o server main.go
[user@machine go]$ ./server -s /tmp/appencryption.sock \
    --service=example \
    --product=servicelayer \
    --expire-after=60m \
    --check-interval=10m \
    --metastore=rdbms \
    --conn='root:my-secret-pw@tcp(0.0.0.0:3306)/asherah' \
    --kms=static
```

Arguments can also be supplied using environment variables

```bash
export ASHERAH_SERVICE_NAME=example
export ASHERAH_PRODUCT_NAME=servicelayer
export ASHERAH_EXPIRE_AFTER=60m
export ASHERAH_CHECK_INTERVAL=10m
export ASHERAH_METASTORE_MODE=rdbms
export ASHERAH_CONNECTION_STRING='root:my-secret-pw@tcp(0.0.0.0:3306)/asherah'
export ASHERAH_KMS_MODE=static

go run main.go -s /tmp/appencryption.sock
```

## Configuring the server
Configuration options are provided via command-line arguments or environment variables. Supported options are as
follows:

```
Usage:
  server [OPTIONS]

Application Options:
  -s, --socket-file=                                       The unix domain socket the server will listen on (default:
                                                           /tmp/appencryption.sock)

Asherah Options:
      --service=                                           The name of this service [$ASHERAH_SERVICE_NAME]
      --product=                                           The name of the product that owns this service [$ASHERAH_PRODUCT_NAME]
      --expire-after=                                      The amount of time a key is considered valid [$ASHERAH_EXPIRE_AFTER]
      --check-interval=                                    The amount of time before cached keys are considered stale
                                                           [$ASHERAH_CHECK_INTERVAL]
      --metastore=[rdbms|dynamodb|memory]                  Determines the type of metastore to use for persisting keys
                                                           [$ASHERAH_METASTORE_MODE]
      --conn=                                              The database connection string (required if --metastore=rdbms)
                                                           [$ASHERAH_CONNECTION_STRING]
      --replica-read-consistency=[eventual|global|session] Required for Aurora sessions using write forwarding (if --metastore=rdbms)
                                                           [$ASHERAH_REPLICA_READ_CONSISTENCY]
      --enable-region-suffix                               Configure the metastore to use regional suffixes (only supported by
                                                           --metastore=dynamodb) [$ASHERAH_ENABLE_REGION_SUFFIX]
      --dynamodb-endpoint=                                 An optional endpoint URL (hostname only or fully qualified URI) (only
                                                           supported by --metastore=dynamodb) [$ASHERAH_DYNAMODB_ENDPOINT]
      --dynamodb-region=                                   The AWS region for DynamoDB requests (defaults to globally configured region)
                                                           (only supported by --metastore=dynamodb) [$ASHERAH_DYNAMODB_REGION]
      --dynamodb-table-name=                               The table name for DynamoDB (only supported by --metastore=dynamodb)
                                                           [$ASHERAH_DYNAMODB_TABLE_NAME]
      --kms=[aws|static]                                   Configures the master key management service (default: aws)
                                                           [$ASHERAH_KMS_MODE]
      --region-map=                                        A comma separated list of key-value pairs in the form of
                                                           REGION1=ARN1[,REGION2=ARN2] (required if --kms=aws) [$ASHERAH_REGION_MAP]
      --preferred-region=                                  The preferred AWS region (required if --kms=aws) [$ASHERAH_PREFERRED_REGION]

Help Options:
  -h, --help                                               Show this help message
```
