# Metastore
Asherah handles the storage of Intermediate and System Keys in its "Metastore" which is pluggable, and hence provides a flexible architecture.

* [Metastore Implementations](#metastore-implementations)
  * [RDBMS](#rdbms)
  * [AWS DynamoDB](#dynamodb)
    * [Permissions](#permissions)
  * [In-memory](#in-memory)
* [Disaster Recovery](#disaster-recovery)
* [Revoking Keys](#revoking-keys)

### Metastore Implementations

#### RDBMS
``` sql
CREATE TABLE encryption_key (
  id             VARCHAR(255) NOT NULL,
  created        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  key_record     TEXT         NOT NULL,
  PRIMARY KEY (id, created),
  INDEX (created)
);
```
**NOTE:** This schema should work with most of the database vendors. We have run our test suites against MySQL. As vendor-specific issues are reported, additional schemas/information will be added as needed.

##### Row Data Size Estimates
The estimates provided are based on examples using product id, system id, and partition id with lengths of 11, 13, and 10 bytes respectively in MySQL.

The row data size estimates were calculated by using the below query against actual data. Note that this **does not** account for index size.

``` sql
-- varchar 255 has 1 byte overhead, timestamp w/ no fractional seconds uses 4 bytes, and text has 2 bytes overhead
select id, created, (1 + octet_length(id) + 4 + 2 + octet_length(key_record)) as row_data_size from encryption_key;
```

**IntermediateKey**: 253 bytes
**SystemKey**: 1227 bytes (assumes AWS KMS with 2 regions. each additional region adds ~494 bytes)


#### DynamoDB

**Default Table Name**: EncryptionKey
**Partition/Hash Key**: Id (string)
**Sort/Range Key**: Created (number)

AWS CLI Example:

``` console
aws dynamodb create-table \
 --table-name EncryptionKey \
 --key-schema \
   AttributeName=Id,KeyType=HASH \
   AttributeName=Created,KeyType=RANGE \
 --attribute-definitions \
   AttributeName=Id,AttributeType=S \
   AttributeName=Created,AttributeType=N \
<billing mode / provisioned throughput setup>
```

##### Permissions

Next, you'll need to ensure Asherah has sufficient permissions to interact with the table. The following example creates
a new customer managed policy that allows any attached user access to the above table.

```console
$ aws iam create-policy --policy-name asherah-dynamodb-access --policy-document file://policy.json
{
    "Policy": {
        "PolicyName": "asherah-dynamodb-access",
        "PolicyId": "ANPAWOYE3S3ESQKWJRFZW",
        "Arn": "arn:aws:iam::123456789012:policy/asherah-dynamodb-access",
        "Path": "/",
        "DefaultVersionId": "v1",
        "AttachmentCount": 0,
        "PermissionsBoundaryUsageCount": 0,
        "IsAttachable": true,
        "CreateDate": "2021-01-12T21:57:05+00:00",
        "UpdateDate": "2021-01-12T21:57:05+00:00"
    }
}
```

The file `policy.json` provided as the policy document is a JSON document in the current directory:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Action": [
                "dynamodb:BatchGetItem",
                "dynamodb:BatchWriteItem",
                "dynamodb:ConditionCheckItem",
                "dynamodb:PutItem",
                "dynamodb:DescribeTable",
                "dynamodb:DeleteItem",
                "dynamodb:GetItem",
                "dynamodb:Scan",
                "dynamodb:Query",
                "dynamodb:UpdateItem"
            ],
            "Resource": "arn:aws:dynamodb:*:12345789012:table/EncryptionKey",
            "Effect": "Allow"
        }
    ]
}
```

For more information on creating policies using the AWS CLI, see
[create-policy](https://awscli.amazonaws.com/v2/documentation/api/latest/reference/iam/create-policy.html) in
AWS CLI Command Reference.

##### Item Data Size Estimates
The estimates provided are based on examples using product id, system id, and partition id with lengths of 11, 13, and 10 bytes respectively.

The item data size estimates were calculated by using https://zaccharles.github.io/dynamodb-calculator/ (client-side calculation, no network calls observed).

**IntermediateKey**: 240 bytes
**SystemKey**: 1227 bytes (assumes AWS KMS with 2 regions. each additional region adds ~494 bytes)

#### In-memory (FOR TESTING ONLY)
Asherah also supports an in-memory metastore but that ***should only be used for testing purposes***.

### Disaster Recovery

Ensure that you have proper backup procedures and policies to prevent accidental deletion of keys from the metastore.
A loss of keys from the metastore can render your data unreadable.

### Revoking keys

If there is a need for irregular rotation of keys (e.g. suspected compromise of keys) there is support for marking keys
as "revoked".

We have created helper python scripts for the above metastore implementations. Usage details on how to run them can
be found [here](../scripts).
