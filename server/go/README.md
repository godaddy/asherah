# Asherah Server - Go

## Running the server
This assumes mysql is running on localhost and a preexisting asherah database

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

go run main -s /tmp/appencryption.sock
```

## Configuring the server
Configuration options are provided via command-line arguments or environment variables. Supported options are as
follows:

```
Usage:
  server [OPTIONS]

Application Options:
  -s, --socket-file=                      The unix domain socket the server will listen on (default:
                                          /tmp/appencryption.sock)

Asherah Options:
      --service=                          The name of this service [$ASHERAH_SERVICE_NAME]
      --product=                          The name of the product that owns this service [$ASHERAH_PRODUCT_NAME]
      --expire-after=                     The amount of time a key is considered valid [$ASHERAH_EXPIRE_AFTER]
      --check-interval=                   The amount of time before cached keys are considered stale
                                          [$ASHERAH_CHECK_INTERVAL]
      --metastore=[rdbms|dynamodb|memory] Determines the type of metastore to use for persisting keys
                                          [$ASHERAH_METASTORE_MODE]
      --conn=                             The database connection string (required if --metastore=rdbms)
                                          [$ASHERAH_CONNECTION_STRING]
      --kms=[aws|static]                  Configures the master key management service (default: aws)
                                          [$ASHERAH_KMS_MODE]
      --region-map=                       A comma separated list of key-value pairs in the form of
                                          REGION1=ARN1[,REGION2=ARN2] (required if --kms=aws) [$ASHERAH_REGION_MAP]
      --preferred-region=                 The preferred AWS region (required if --kms=aws) [$ASHERAH_PREFERRED_REGION]

Help Options:
  -h, --help                              Show this help message
```
