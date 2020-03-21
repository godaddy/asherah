# Asherah Server - Go

## Running the server
This example assumes mysql is running on localhost and a preexisting asherah database

```
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
