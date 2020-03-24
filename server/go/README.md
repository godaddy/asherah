# Asherah Server - Go

## Running the server
This example assumes mysql is running on localhost and a preexisting asherah database

```console
$ go build -o server main.go
$ ./server -s /tmp/appencryption.sock \
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
