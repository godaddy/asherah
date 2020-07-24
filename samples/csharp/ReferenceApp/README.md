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
[user@machine ReferenceApp]$ dotnet bin/Debug/netcoreapp2.0/ReferenceApp.dll \ 
  --metastore-type ADO \
  --ado-connection-string <AdoConnectionString> \
  --kms-type AWS \
  --preferred-region <preferredRegion> \
  --region-arn-tuples <region1=arn_of_kms_key_for_region1,region2=arn_of_kms_key_for_region2, ...> \
  --iterations 100
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

#### Global Tables

To use Global Tables, the above table needs to be created with few modifications.

More details about how to create a Global Table can be found in the
[AWS Developer Guide](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/globaltables.tutorial.html)

Example run using DynamoDB metastore with key suffixes enabled (for Global Tables), a custom table name, and a
local DynamoDB endpoint.

```console
[user@machine ReferenceApp]$ dotnet bin/Debug/netcoreapp2.0/ReferenceApp.dll \
  --metastore-type DYNAMODB \
  --enable-key-suffix \
  --dynamodb-endpoint http://localhost:8000 \
  --dynamodb-signing-region us-west-2 \
  --dynamodb-table-name MyGlobalTable
``` 

## Configuring the Reference App
Configuration options are provided via command-line arguments. Supported options are as
follows:

```console
  -m, --metastore-type           (Default: MEMORY) Type of metastore to use. Enum values: MEMORY, ADO, DYNAMODB

  -e, --dynamodb-endpoint        The DynamoDb service endpoint (only supported by DYNAMODB)

  -r, --dynamodb-region          The AWS region for DynamoDB requests (only supported by DYNAMODB)

  -t, --dynamodb-table-name      The table name for DynamoDb (only supported by DYNAMODB)

  -s, --key-suffix               Configure the metastore to use key suffixes (only supported by DYNAMODB)

  -a, --ado-connection-string    ADO connection string to use for an ADO metastore. Required for ADO metastore.

  -k, --kms-type                 (Default: STATIC) Type of key management service to use. Enum values: STATIC, AWS

  -p, --preferred-region         Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.

  -t, --region-arn-tuples        Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.

  -i, --iterations               (Default: 1) Number of encrypt/decrypt iterations to run

  -c, --enable-cw                Enable CloudWatch Metrics output

  -d, --drr                      DRR to be decrypted

  --help                         Display this help screen.

  --version                      Display version information.
```

TODO: Add link to Sceptre template example  
