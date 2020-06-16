#!/usr/bin/env bash

set -e

export PATH=$GOPATH/bin:$GOROOT/bin:$PATH

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
pip3.7 install -r requirements.txt
echo "----------Encrypting payload with Go sidecar and python client----------"
export ASHERAH_EXPIRE_AFTER=60m
export ASHERAH_CHECK_INTERVAL=10m
export ASHERAH_METASTORE_MODE=rdbms
export ASHERAH_CONNECTION_STRING='root:Password123@tcp(127.0.0.1:3306)/testdb'
cd ../../../server/go/
go run main.go -s /tmp/appencryption.sock &
ASHERAH_GO_SIDECAR_PID=$!
cd -
sleep 10
behave -D FILE=/tmp/sidecar_go_encrypted features/encrypt.feature
kill $ASHERAH_GO_SIDECAR_PID
rm -rf /tmp/appencryption.sock

echo "---------Encrypting payload with Java sidecar and python client---------"
# TODO : Remove this after unifying configurations
# https://github.com/godaddy/asherah/issues/143
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=10
export ASHERAH_METASTORE_MODE=jdbc
export ASHERAH_CONNECTION_STRING='jdbc:mysql://127.0.0.1:3306/testdb?user=root&password=Password123'
find ~/.m2/ -name '*grpc-server*dependencies.jar' | xargs java -jar &
ASHERAH_JAVA_SIDECAR_PID=$!
sleep 10
behave -D FILE=/tmp/sidecar_java_encrypted features/encrypt.feature
kill $ASHERAH_JAVA_SIDECAR_PID
rm -rf /tmp/appencryption.sock
