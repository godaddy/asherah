#!/usr/bin/env bash
set -e


# Run encrypt tests for all languages
cd java
echo "Encrypting payload using Java"
mvn -Dtest=RunEncryptTest test
cd ..

cd csharp
echo "Encrypting payload using C#"
cp ../features/* .
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.EncryptDataUsingRDBMSMetastoreAndStaticKMSFeature.EncryptingData --no-build
cd ..

cd go
echo "Encrypting payload using Go"
export PATH=$GOPATH/bin:$GOROOT/bin:$PATH
godog ../features/encrypt.feature
cd ..

# Run decrypt tests for all languages
cd java
echo "Decrypting data using Java"
mvn -Dtest=RunDecryptTest test
cd ..

cd csharp
echo "Decrypting data using C#"
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.DecryptDataUsingRDBMSMetastoreAndStaticKMSFeature.DecryptingData --no-build
rm *.feature
cd ..

cd go
echo "Decrypting data using Go"
godog ../features/decrypt.feature
cd ..
