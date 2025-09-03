# Asherah Go referenceapp

Sample application that can be used as a guide to using the GO AppEncryption SDK.

Current implementation shows a simple encrypt/decrypt use of the library. The Metastore and KMS implementations used are
based on parameters specified via command-line args. A basic Crypto Policy is being used.

This sample demonstrates current best practices using AWS SDK v2 for AWS service integration.

Future updates as the library evolves:

- Additional usage example utilizing more robust metadata and data storage handling by the library

Table of Contents
=================

  * [How to Run Reference App](#how-to-run-reference-app)
  * [General Notes](#general-notes)
  * [External Resource Setup](#external-resource-setup)
    * [Using an RDBMS Metastore](#using-an-rdbms-metastore)
    * [Using a DynamoDB Metastore](#using-a-dynamodb-metastore)
  * [Configuring the Reference App](#configuring-the-reference-app)

## How to Run Reference App

Example showing CLI options:

```console
[user@machine referenceapp]$ go run . --help
```

Example run using defaults (in-memory metastore, static KMS):

```console
[user@machine referenceapp]$ go run .
 ```

Example run using RDBMS metastore with AWS KMS and specify payload to encrypt:

```console
[user@machine referenceapp]$ go run . \
--metastore RDBMS \
--conn "<rdbms connection string>" \
--kms-type AWS \
--region-arn-tuples <region1=arn_of_kms_key_for_region1,region2=arn_of_kms_key_for_region2, ...>\
--preferred-region <preferredRegion>\
--payload-to-encrypt "some super secret value"
 ```

## General Notes

- The `SessionFactory` is intended to have as large of scope as possible to leverage caching and minimize resource
usage. If your service/app is responsible for only one service, then ideally use a global instance. If it hosts multiple
services, then ideally have one instance per product id and system id pairing.
- Always remember to close the `SessionFactory` before exiting the service to ensure that all resources held by the
factory, including the cache, are disposed of properly.


## External Resource Setup
To run the reference app with external metastores or KMS implementations, some additional setup may be required as
specified below.

### Using an RDBMS Metastore

To use the RDBMS Metastore included with Asherah, the following table should be created:

``` sql
CREATE TABLE encryption_key (
  id             VARCHAR(255) NOT NULL,
  created        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  key_record     TEXT         NOT NULL,
  PRIMARY KEY (id, created),
  INDEX (created)
);

```
**NOTE**: The above schema is known to work properly with MySQL. Adjust the schema data types for other database vendors
accordingly. As we officially integrate with teams using other database vendors, we will add more vendor-specific
schemas as needed.

### Using a DynamoDB Metastore
To use the DynamoDB Metastore included with Asherah, the following table should be created:

``` console
[user@machine go-referenceapp]$ aws dynamodb create-table \
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

**NOTE**: To use a custom name for your DynamoDB table, replace `EncryptionKey` in the above command with your preferred
table name. You will also need to configure the Reference App to use your custom table via the `--dynamodb-table-name`
flag (example below).

#### Global Tables

To use Global Tables, the above table needs to be created with few modifications.

More details about how to create a Global Table can be found in the [AWS Developer Guide](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/globaltables.tutorial.html)

Example run using DynamoDB metastore with regional suffixes enabled (for Global Tables), a custom table name, and a
local DynamoDB endpoint.

```console
[user@machine referenceapp]$ go run . \
--metastore DYNAMODB \
--enable-region-suffix \
--dynamodb-endpoint http://localhost:8000 \
--dynamodb-table-name MyCustomTableName \
--payload-to-encrypt "some super secret value"
 ```

## Configuring the Reference App
Configuration options are provided via command-line arguments. Supported options are as
follows:

```
Usage:
  main [OPTIONS]

Application Options:
  -d, --drr-to-decrypt=       DRR to be decrypted
  -p, --payload-to-encrypt=   payload to be encrypted
  -m, --metastore=            Configure what metastore to use (DYNAMODB/SQL/MEMORY)
  -x, --enable-region-suffix  Configure the metastore to use regional suffixes (only supported by DYNAMODB)
  -c, --conn=                 MySQL connection String
      --dynamodb-endpoint=    An optional endpoint URL (hostname only or fully qualified URI) (only supported by
                              DYNAMODB)
      --dynamodb-region=      The AWS region for DynamoDB requests (only supported by DYNAMODB) (default: us-west-2)
      --dynamodb-table-name=  The table name for DynamoDB (only supported by DYNAMODB)
      --kms-type=             Type of key management service to use (AWS/STATIC)
      --preferred-region=     Preferred region to use for KMS if using AWS KMS. Required for AWS KMS.
      --region-arn-tuples=    Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS.
      --partition-id=         The partition id to use for client sessions

Help Options:
  -h, --help                  Show this help message
```

TODO: Add link to Sceptre template example
