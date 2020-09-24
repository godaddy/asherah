# Key Management Service

Asherah requires a Key Management Service (KMS) to generate the top level Master Key and to encrypt the System Keys.
The key management service is pluggable which provides you a flexible architecture. It enables you to use an HSM for
providing the Master Key or staying cloud agnostic if using a hosted key management service.

* [Supported Key Management Systems](#supported-key-management-systems)
    * [AWS KMS](#aws-kms)
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

### Static KMS (FOR TESTING ONLY)

The SDK also supports a static KMS but it ***should never be used in production***.

## Disaster Recovery

Ensure that you have policies to prevent accidental deletion of a KMS key. **Removal of a KMS key will render System Keys
unusable which may, consequently, render data unreadable.**
