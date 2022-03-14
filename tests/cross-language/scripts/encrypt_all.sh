#!/usr/bin/env bash

set -ex

export PATH=$GOPATH/bin:$GOROOT/bin:$PATH

TEST_DB_PORT="${TEST_DB_PORT:-3306}"
TEST_DB_NAME="${TEST_DB_NAME:-testdb}"
TEST_DB_USER="${TEST_DB_USER:-root}"
TEST_DB_PASSWORD="${TEST_DB_PASSWORD:-Password123}"
ASHERAH_SOCKET_FILE="${ASHERAH_SOCKET_FILE:-/tmp/appencryption.sock}"

# Run encrypt tests for all languages
cd java
echo "----------------------Encrypting payload using Java---------------------"
mvn -Drevision=${JAVA_AE_VERSION} -Dtest=RunEncryptTest test
cd ..

cd csharp
echo "----------------------Encrypting payload using C#-----------------------"
dotnet test --configuration Release --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.EncryptDataUsingRDBMSMetastoreAndStaticKMSFeature.EncryptingData --no-build
cd ..

cd go
echo "----------------------Encrypting payload using Go-----------------------"
godog ../features/encrypt.feature
cd ..

cd sidecar
pip3.8 install -r requirements.txt
echo "----------Encrypting payload with Go sidecar and python client----------"
export ASHERAH_EXPIRE_AFTER=60m
export ASHERAH_CHECK_INTERVAL=10m
export ASHERAH_METASTORE_MODE=rdbms
export ASHERAH_CONNECTION_STRING="${TEST_DB_USER}:${TEST_DB_PASSWORD}@tcp(127.0.0.1:${TEST_DB_PORT})/${TEST_DB_NAME}"

# Start the Go server
cd ../../../server/go/
go run main.go -s ${ASHERAH_SOCKET_FILE} &
ASHERAH_GO_SIDECAR_PID=$!
cd -

# Wait for the socket to exist
while [ ! -S ${ASHERAH_SOCKET_FILE} ]; do sleep 1; done

# Run the tests
behave -D FILE=/tmp/sidecar_go_encrypted features/encrypt.feature
kill $ASHERAH_GO_SIDECAR_PID
rm -rf ${ASHERAH_SOCKET_FILE}

echo "---------Encrypting payload with Java sidecar and python client---------"
# TODO : Remove this after unifying configurations
# https://github.com/godaddy/asherah/issues/143
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=10
export ASHERAH_METASTORE_MODE=jdbc
export ASHERAH_CONNECTION_STRING="jdbc:mysql://127.0.0.1:${TEST_DB_PORT}/${TEST_DB_NAME}?user=${TEST_DB_USER}&password=${TEST_DB_PASSWORD}"

# Start the Java server
find ~/.m2/ -name '*grpc-server*dependencies.jar' | xargs java -jar &
ASHERAH_JAVA_SIDECAR_PID=$!

# Wait for the socket to exist
while [ ! -S ${ASHERAH_SOCKET_FILE} ]; do sleep 1; done

# Run the tests
behave -D FILE=/tmp/sidecar_java_encrypted features/encrypt.feature
kill $ASHERAH_JAVA_SIDECAR_PID
rm -rf ${ASHERAH_SOCKET_FILE}

# Run the Node encrypt test
cd ../node/ && ./run-encrypt.sh
