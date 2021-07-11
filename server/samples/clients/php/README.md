# gRPC Client (PHP) for Asherah Server

A simple client application that demonstrates integrating with Asherah Server via a generated gRPC client.

## Running the server

This example runs Asherah as a non-root user, as many PHP web applications may be running this way behind Apache or nginx.

Build the Asherah Server as normal:

```console
[user@machine php]$ docker build -t asherah_server_build ../../../go
```

Then build the custom non-root image:

```console
[user@machine php]$ docker build -t asherah_server -f Asherah.Dockerfile .
```

Create a new volume for the images to share:

```console
[user@machine php]$ docker volume create asherah_socket
```

Finally, start up Asherah Server as a non-root user:

```console
[user@machine php]$ docker run -d --rm \
    --name asherah_server \
    --cap-add IPC_LOCK \
    -e ASHERAH_SERVICE_NAME=php \
    -e ASHERAH_PRODUCT_NAME=sample \
    -e ASHERAH_EXPIRE_AFTER=60m \
    -e ASHERAH_CHECK_INTERVAL=10m \
    -e ASHERAH_METASTORE_MODE=memory \
    -e ASHERAH_KMS_MODE=static \
    -v asherah_socket:/tmp \
    asherah_server --socket-file /tmp/appencryption.sock
```

## Running the client

**Optional** Rebuild the PHP client library:

```console
[user@machine php]$ docker run --rm \
    -v $(pwd)/../../../protos:/protos \
    -v $(pwd)/:/out \
    -w /out \
    znly/protoc \
    --plugin=protoc-gen-grpc=/usr/bin/grpc_php_plugin \
    --php_out=./lib \
    --grpc_out=./lib \
    appencryption.proto \
    --proto_path=/protos
```

Build the sample application:

```console
[user@machine php]$ docker build -t asherah_php_client .
```

Ensure the Asherah Server is running locally and listening on `unix:///tmp/appencryption.sock` and run:

```console
[user@machine php]$ docker run --rm \
    --name asherah_php_client \
    -v asherah_socket:/sock \
    asherah_php_client --socket unix:///sock/appencryption.sock
starting test
starting session for partitionid-5eac987ca5f69
encrypting: my "secret" data - 5eac987ca5f67
received DRR
decrypting DRR
received decrypted data: my "secret" data - 5eac987ca5f67
test completed successfully
```
