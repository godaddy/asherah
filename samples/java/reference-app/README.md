# Asherah Java Reference App

Sample application that can be used as a guide to using the Java Asherah SDK.

Current implementation shows a simple encrypt/decrypt use of the library. The Metastore and KMS implementations used are based on parameters specified via command-line args. A basic Crypto Policy is being used.

Future updates as the library evolves:

- Additional usage example utilizing more robust metadata and data storage handling by the library

Table of Contents
=================

  * [How to Run Reference App](#how-to-run-reference-app)
  * [General Notes](#general-notes)
  * [External Resource Setup](#external-resource-setup)
    * [Using a JDBC Metastore](#using-a-jdbc-metastore)
    * [Using a DynamoDB Metastore](#using-a-dynamodb-metastore)

## How to Run Reference App

Running `mvn clean install` will generate an executable JAR.

Example showing CLI options:

```console
[user@machine reference-app]$ java -jar target/referenceapp-1.0.0-SNAPSHOT-jar-with-dependencies.jar --help
```

Example run using defaults (in-memory metastore, static KMS, console metrics only):

```console
[user@machine reference-app]$ java -jar target/referenceapp-1.0.0-SNAPSHOT-jar-with-dependencies.jar 
 ```

Example run using MySQL metastore, AWS KMS, CloudWatch metrics and 100 iterations:

```console
[user@machine reference-app]$ java -jar target/referenceapp-1.0.0-SNAPSHOT-jar-with-dependencies.jar --metastore-type JDBC --jdbc-url 'jdbc:mysql://localhost/test?user=root&password=password' --kms-type AWS --preferred-region us-west-2 --region-arn-tuples us-west-2=<YOUR_USWEST2_ARN>,us-east-1=<YOUR_USEAST1_ARN> --enable-cw --iterations 100
```


## General Notes

- Both the `SessionFactory` and the `Session` classes implement the AutoCloseable interface for easy resource
 management.
- The `SessionFactory` class is intended to have as large of scope as possible to leverage caching and minimize resource usage. If your service/app is responsible for only one service, then ideally use a global instance. If it hosts multiple services, then ideally have one instance per product id and system id pairing.

## External Resource Setup
To run the reference app with external metastores or KMS implementations, some additional setup may be required as specified below.

### Using a JDBC Metastore

To use the JDBC-compliant Metastore included with the App Encryption library, the following table should be created:

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
[user@machine AppEncryptionJava]$ aws dynamodb create-table \
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
