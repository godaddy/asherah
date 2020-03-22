# Asherah Go referenceapp

Sample application that can be used as a guide to using the GO AppEncryption SDK.

Current implementation shows a simple encrypt/decrypt use of the library. The Metastore and KMS implementations used are
based on parameters specified via command-line args. A basic Crypto Policy is being used.

Future updates as the library evolves:

- Additional usage example utilizing more robust metadata and data storage handling by the library

Table of Contents
=================

  * [How to Run Reference App](#how-to-run-reference-app)
  * [General Notes](#general-notes)
  * [External Resource Setup](#external-resource-setup)
    * [Using an RDBMS Metastore](#using-an-rdbms-metastore)
    * [Using a DynamoDB Metastore](#using-a-dynamodb-metastore)

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

TODO: Add link to Sceptre template example  
TODO: Add multi-region info if/when we handle it
