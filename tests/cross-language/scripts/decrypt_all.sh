#!/usr/bin/env bash

set -e

export PATH=$GOPATH/bin:$GOROOT/bin:$PATH

# Run decrypt tests for all languages
cd java
echo "----------------------Decrypting data using Java------------------------"
mvn -Drevision=${JAVA_AE_VERSION} -Dtest=RunDecryptTest test
cd ..

cd csharp
echo "----------------------Decrypting data using C#--------------------------"
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.DecryptDataUsingRDBMSMetastoreAndStaticKMSFeature.DecryptingData
cd ..

cd go
echo "----------------------Decrypting data using Go--------------------------"
godog ../features/decrypt.feature
cd ..

cd sidecar
pip3.7 install -r requirements.txt
echo "------------Decrypting data with Go sidecar and python client-----------"
export ASHERAH_EXPIRE_AFTER=60m
export ASHERAH_CHECK_INTERVAL=10m
export ASHERAH_METASTORE_MODE=rdbms
export ASHERAH_CONNECTION_STRING='root:Password123@tcp(127.0.0.1:3306)/testdb'
cd ../../../server/go/
go run main.go -s /tmp/appencryption.sock &
ASHERAH_GO_SIDECAR_PID=$!
cd -
sleep 10
behave features/decrypt.feature
kill $ASHERAH_GO_SIDECAR_PID
rm -rf /tmp/appencryption.sock

echo "-----------Decrypting data with Java sidecar and python client----------"
# TODO : Remove this after unifying configurations
# https://github.com/godaddy/asherah/issues/143
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=10
export ASHERAH_METASTORE_MODE=jdbc
export ASHERAH_CONNECTION_STRING='jdbc:mysql://127.0.0.1:3306/testdb?user=root&password=Password123'
java -jar /home/circleci/.m2/repository/com/godaddy/asherah/grpc-server/1.0.0-SNAPSHOT/*dependencies.jar &
ASHERAH_JAVA_SIDECAR_PID=$!
sleep 10
behave features/decrypt.feature
kill $ASHERAH_JAVA_SIDECAR_PID
rm -rf /tmp/appencryption.sock
