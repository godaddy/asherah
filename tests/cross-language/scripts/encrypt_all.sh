#!/usr/bin/env bash

set -ex

which go
go version

TEST_DB_PORT="${TEST_DB_PORT:-3306}"
TEST_DB_NAME="${TEST_DB_NAME:-testdb}"
TEST_DB_USER="${TEST_DB_USER:-root}"
TEST_DB_PASSWORD="${TEST_DB_PASSWORD:-Password123}"
ASHERAH_SOCKET_FILE="${ASHERAH_SOCKET_FILE:-/tmp/appencryption.sock}"

# Function to wait for the socket to exist
wait_for_socket() {
    local timeout=60
    local elapsed=0

    echo "Waiting for ${ASHERAH_SOCKET_FILE} to appear (timeout: ${timeout} seconds)"

    while [ ! -S ${ASHERAH_SOCKET_FILE} ]; do
        sleep 1
        elapsed=$((elapsed + 1))
        if [ $elapsed -ge $timeout ]; then
            echo "Socket file did not appear after $timeout seconds"
            exit 1
        fi
    done
}

# Run encrypt tests for all languages
cd java
echo "----------------------Encrypting payload using Java---------------------"
mvn --no-transfer-progress -Drevision=${JAVA_AE_VERSION} -Dtest=RunEncryptTest test
cd ..

cd csharp
echo "----------------------Encrypting payload using C#-----------------------"
dotnet test --configuration Release --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.EncryptDataUsingRDBMSMetastoreAndStaticKMSFeature.EncryptingData --no-build
cd ..

cd go
echo "----------------------Encrypting payload using Go-----------------------"
go test -v -test.run '^TestEncryptFeatures$' -godog.paths=../features/encrypt.feature
cd ..

cd sidecar
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

wait_for_socket

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

wait_for_socket

# Run the tests
behave -D FILE=/tmp/sidecar_java_encrypted features/encrypt.feature
kill $ASHERAH_JAVA_SIDECAR_PID
rm -rf ${ASHERAH_SOCKET_FILE}
