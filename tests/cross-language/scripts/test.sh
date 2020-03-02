#!/usr/bin/env bash
set -e

mkdir -p encrypted_files

cd java
mvn -Dtest=RunEncryptTest test
mv java_encrypted ../encrypted_files/

cd ..

cd csharp
cp ../features/* .
dotnet test --filter FullyQualifiedName=csharp.EncryptDataUsingInMemoryMetastoreStaticKMSFeature.EncryptingData
mv bin/Debug/csharp_encrypted ../encrypted_files/

cd ..

#cd go
# run go encrypt tests here
