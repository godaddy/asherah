#!/usr/bin/env bash
set -e

mkdir -p encrypted_files
ls -a ~/.nuget/packages
ls -ltr /home/circleci/.m2/repository

cd java
ls -ltr
mvn -Dtest=RunEncryptTest test
ls -ltr
mv java_encrypted ../encrypted_files

cd csharp
cp ../features/* .
dotnet test --filter FullyQualifiedName=csharp.EncryptDataUsingInMemoryMetastoreStaticKMSFeature.EncryptingData
mv bin/Debug/csharp_encrypted ../encrypted_files/

cd ../go
# run go encrypt tests here