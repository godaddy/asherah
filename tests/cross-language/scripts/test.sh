#!/usr/bin/env bash
set -e

mkdir -p encrypted_files

# Run encrypt tests for all languages
echo "Encrypting payload using Java"
cd java
mvn -Dtest=RunEncryptTest test

cd ..

echo "Encrypting payload using C#"
cd csharp
cp ../features/* .
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.CrossLanguage.CSharp.EncryptDataUsingAnRDBMSMetastoreAndStaticKMSFeature.EncryptingData

cd ..

#cd go
# run go encrypt tests here

# Run decrypt tests for all languages

echo "Decrypting data using Java"
cd java
mvn -Dtest=RunDecryptTest test

cd ..

echo "Decrypting data using C#"
cd csharp
dotnet test --filter FullyQualifiedName=GoDaddy.Asherah.CrossLanguage.CSharp.DecryptDataUsingAnRDBMSMetastoreAndStaticKMSFeature.DecryptingData
rm encrypt.feature
rm decrypt.feature

cd .. 

#cd go
# run go encrypt tests here

rm -rf encrypted_files

