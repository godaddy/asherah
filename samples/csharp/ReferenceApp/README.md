# Asherah C# ReferenceApp

Sample application that can be used as a guide to using the C# Asherah SDK.

Current implementation shows a simple encrypt/decrypt use of the library. The Metastore and KMS implementations used are based on parameters specified via command-line args. A basic Crypto Policy is being used.

Future updates as the library evolves:

- Additional usage example utilizing more robust metadata and data storage handling by the library

Table of Contents
=================

  * [How to Run Reference App](#how-to-run-reference-app)
    * [Using a Docker read-only container](#using-a-docker-read-only-container)
  * [General Notes](#general-notes)
  * [External Resource Setup](#external-resource-setup)
    * [Using an ADO Metastore](#using-an-ado-metastore)
    * [Using a DynamoDB Metastore](#using-a-dynamodb-metastore)

## How to Run Reference App

Running `dotnet publish` will generate the required dll.

Example showing CLI options:

```console
[user@machine ReferenceApp]$ dotnet bin/Debug/netcoreapp2.0/ReferenceApp.dll --help
```

Example run using defaults (in-memory metastore, static KMS, console metrics only):

```console
[user@machine ReferenceApp]$ dotnet bin/Debug/netcoreapp2.0/ReferenceApp.dll
 ```

Example run using ADO persistence and AWS KMS and 100 iterations:

```console
[user@machine ReferenceApp]$ dotnet bin/Debug/netcoreapp2.0/ReferenceApp.dll -m ADO -a <AdoConnectionString> -k AWS -p <preferredRegion> -r <region1=arn_of_kms_key_for_region1,region2=arn_of_kms_key_for_region2, ...> -i 100
 ```
 
### Using a Docker read-only container

The ReferenceApp can be tested/used in a docker container having only the dotnetcore runtime environment.
```console
# Generate the build container docker image
[user@machine ReferenceApp]$ docker build images/build/

# Use the generated image to publish the packages
[user@machine ReferenceApp]$ docker run -it --rm -v $HOME/.nuget:/home/jenkins/.nuget -v "$PWD":/usr/app/src -w /usr/app/src --ulimit memlock=-1:-1 --ulimit core=-1:-1 <generated-image-id> dotnet clean -c Release && dotnet publish -c Release

# Build the runtime docker image using published packages
[user@machine ReferenceApp]$ docker build -f images/runtime/Dockerfile .

# Run the image in a read-only container
[user@machine ReferenceApp]$ docker run -it --read-only <runtime-generated-image-id>
```

## General Notes

- Both the `SessionFactory` and the `Session` session classes implement
  the IDisposable interface for easy resource management.
- The `SessionFactory` class is intended to have as large of scope as possible to leverage caching and minimize resource usage. If your service/app is responsible for only one service, then ideally use a global instance. If it hosts multiple services, then ideally have one instance per product id and system id pairing.

## External Resource Setup
To run the reference app with external metastores or KMS implementations, some additional setup may be required as specified below.

### Using an ADO Metastore

To use the ADO-compliant Metastore included with the App Encryption library, the following table should be created:

``` sql
CREATE TABLE encryption_key (
  id             VARCHAR(255) NOT NULL,
  created        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  key_record     TEXT         NOT NULL,
  PRIMARY KEY (id, created),
  INDEX (created)
);

```
**NOTE**: The above schema is known to work properly with MySQL. Adjust the schema data types for other database vendors accordingly. As we officially integrate with teams using other database vendors, we will add more vendor-specific schemas as needed.

### Using a DynamoDB Metastore
To use the DynamoDB Metastore included with the App Encryption library, the following table should be created:

``` console
[user@machine ReferenceApp]$ aws dynamodb create-table \
 --table-name EncryptionKey \
 --key-schema \
   AttributeName=Id,KeyType=HASH \
   AttributeName=Created,KeyType=RANGE \
 --attribute-definitions \
   AttributeName=Id,AttributeType=S \
   AttributeName=Created,AttributeType=N \
 --provisioned-throughput \
   ReadCapacityUnits=1,WriteCapacityUnits=1
```
TODO: Add link to Sceptre template example
TODO: Add multi-region info if/when we handle it  
