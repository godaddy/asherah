#!/usr/bin/env bash
set -e

mkdir -p encrypted_files

# Run encrypt tests for all languages
cd java
echo "Encrypting payload using Java"
mvn -Dtest=RunEncryptTest test
cd ..

cd csharp
echo "Encrypting payload using C#"
cp ../features/* .
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.EncryptDataUsingRDBMSMetastoreAndStaticKMSFeature.EncryptingData
cd ..

#cd go
# run go encrypt tests here
#cd ..

# Run decrypt tests for all languages
cd java
echo "Decrypting data using Java"
mvn -Dtest=RunDecryptTest test
cd ..

cd csharp
echo "Decrypting data using C#"
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.Cltf.DecryptDataUsingRDBMSMetastoreAndStaticKMSFeature.DecryptingData
rm *.feature
cd ..

#cd go
# run go encrypt tests here
#cd ..

rm -rf encrypted_files
