#!/usr/bin/env bash

set -e

# export PATH=${GOROOT}/bin:${PATH}
go version

export ASHERAH_SERVICE_NAME=service
export ASHERAH_PRODUCT_NAME=product
export ASHERAH_KMS_MODE=static
export ASHERAH_METASTORE_MODE=memory

# Test Go server
echo "------------Testing clients using go server------------"
cd ../../go
export ASHERAH_EXPIRE_AFTER=60m
export ASHERAH_CHECK_INTERVAL=10m
go mod download
go run main.go -s /tmp/appencryption.sock &
ASHERAH_GO_SIDECAR_PID=$!
cd -

# sleep until socket is created, fail after 30 seconds
timeout 30 bash -c 'until [ -S /tmp/appencryption.sock ]; do echo waiting for socket file...; sleep 1; done' || exit 1

cd python
pip3.7 install -r requirements.txt
python3.7 appencryption_client.py
cd ..

cd node
npm install
node appencryption_client.js
cd ..

kill $ASHERAH_GO_SIDECAR_PID
rm -rf /tmp/appencryption.sock

# Test Java server
echo "------------Testing clients using java server----------"
# TODO : Remove this after unifying configurations
# https://github.com/godaddy/asherah/issues/143
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=10
find ~/.m2 -name '*grpc-server*dependencies.jar' | xargs java -jar &
ASHERAH_JAVA_SIDECAR_PID=$!

# sleep until socket is created, fail after 30 seconds
timeout 30 bash -c 'until [ -S /tmp/appencryption.sock ]; do echo waiting for socket file...; sleep 1; done' || exit 1

cd python
python3.7 appencryption_client.py
cd ..

cd node
node appencryption_client.js
cd ..

kill $ASHERAH_JAVA_SIDECAR_PID
rm -rf /tmp/appencryption.sock
