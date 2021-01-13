# Key Management Service

Asherah requires a Key Management Service (KMS) to generate the top level Master Key and to encrypt the System Keys.
The key management service is pluggable which provides you a flexible architecture. It enables you to use an HSM for
providing the Master Key or staying cloud agnostic if using a hosted key management service.

* [Supported Key Management Systems](#supported-key-management-systems)
    * [AWS KMS](#aws-kms)
      * [Creating Keys](#creating-an-aws-kms-key)
      * [Permissions](#permissions)
    * [Static Key Management](#static-kms-for-testing-only)
* [Disaster Recovery](#disaster-recovery)

## Supported Key Management Systems

### AWS KMS

Asherah provides multi-region support for [AWS KMS](https://docs.aws.amazon.com/kms/latest/developerguide/index.html).
You can encrypt data in one region and decrypt it using the keys from another region.

To setup the service, all you need is a map/dictionary of regions and corresponding ARNs and a preferred region. The SDK
will give priority to the preferred region while attempting decryption, but will fall back to other regions, if required.

The key encryption key for each region is stored as part of the meta information for the System Key. Any of those KEKs
can be used to decrypt the System Key.

The meta information is stored internally in the following format:

```javascript
{
  "encryptedKey": "<base64_encoded_bytes>",
  "kmsKeks": [
    {
      "region": "<aws_region>",
      "arn": "<arn>",
      "encryptedKek": "<base64_encoded_bytes>"
    },
    ...
  ]
}
```

NOTE: In case of a local region KMS failure, expect higher latency as a different region's KMS ARN will be used to
decrypt the System Key. Keep in mind this should be rare since System Keys should be cached to further reduce
likelihood of this.

#### Creating an AWS KMS Key

You can create a new key using the AWS CLI. See the
[AWS Develper Guide](https://docs.aws.amazon.com/kms/latest/developerguide/create-keys.html) for more.

```console
$ aws kms create-key
{
    "KeyMetadata": {
        "AWSAccountId": "123456789012",
        "KeyId": "1234abcd-56ef-78ab-90cd-1a2b3c4d5e6f",
        "Arn": "arn:aws:kms:us-west-2:123456789012:key/1234abcd-56ef-78ab-90cd-1a2b3c4d5e6f",
        "CreationDate": "2021-01-12T12:00:14.916000-08:00",
        "Enabled": true,
        "Description": "",
        "KeyUsage": "ENCRYPT_DECRYPT",
        "KeyState": "Enabled",
        "Origin": "AWS_KMS",
        "KeyManager": "CUSTOMER",
        "CustomerMasterKeySpec": "SYMMETRIC_DEFAULT",
        "EncryptionAlgorithms": [
            "SYMMETRIC_DEFAULT"
        ]
    }
}
```

#### Permissions

Next, you'll need to ensure Asherah has sufficient permissions to interact with the key. The following example creates a
new customer managed policy that allows any attached user access to the above key.

```console
$ aws iam create-policy --policy-name asherah-kms-access --policy-document file://policy.json
{
    "Policy": {
        "PolicyName": "asherah-kms-access",
        "PolicyId": "ZXR6A36LTYANPAI7NJ5UV",
        "Arn": "arn:aws:iam::123456789012:policy/asherah-kms-policy",
        "Path": "/",
        "DefaultVersionId": "v1",
        "AttachmentCount": 0,
        "PermissionsBoundaryUsageCount": 0,
        "IsAttachable": true,
        "CreateDate": "2021-01-12T21:05:15+00:00",
        "UpdateDate": "2021-01-12T21:05:15+00:00"
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
                "kms:Decrypt",
                "kms:DescribeKey",
                "kms:Encrypt",
                "kms:GenerateDataKey*",
                "kms:ReEncrypt*"
            ],
            "Resource": [
                "arn:aws:kms:us-west-2:123456789012:key/1234abcd-56ef-78ab-90cd-1a2b3c4d5e6f"
            ],
            "Effect": "Allow"
        }
    ]
}
```

For more information on creating policies using the AWS CLI, see
[create-policy](https://awscli.amazonaws.com/v2/documentation/api/latest/reference/iam/create-policy.html) in
AWS CLI Command Reference.

### Static KMS (FOR TESTING ONLY)

The SDK also supports a static KMS but it ***should never be used in production***.

## Disaster Recovery

Ensure that you have policies to prevent accidental deletion of a KMS key. **Removal of a KMS key will render System Keys
unusable which may, consequently, render data unreadable.**
