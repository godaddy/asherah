# Asherah Server - Java

## Running the server
This example uses static kms and an in-memory metastore

```console
[user@machine java]$ mvn clean install
[user@machine java]$ java -jar <jar-path> --productId=product, --serviceId=service, --uds="/tmp/appencryption.sock"
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - using in-memory metastore...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - using static KMS...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - Server has started
```

The available options are
```console
--jdbc-url=<jdbcUrl>      JDBC URL to use for JDBC metastore. Required for JDBC metastore.
--key-expiration-days=<keyExpirationDays>
                          The number of days after which a key will expire
--kms-type=<kmsType>      Type of key management service to use.
                          Enum values: STATIC, AWS
--metastore-type=<metastoreType>
                          Type of metastore to use. 
                          Enum values: MEMORY, JDBC, DYNAMODB
--preferred-region=<preferredRegion>
                          Preferred region to use for KMS if using AWS KMS.
                          Required for AWS KMS.
--productId=<productId>
                          Specify the product id
--region-arn-tuples=<String=String>[,<String=String>...]
                          Comma separated list of <region>=<kms_arn> tuples.
                          Required for AWS KMS.
--revoke-check-minutes=<revokeCheckMinutes>
                          Sets the cache's TTL to revoke the keys in the cache
--serviceId=<serviceId>   Specify the service id
--session-cache-expire-minutes=<sessionCacheExpireMinutes>
                          Evict the session from cache after some minutes.
--session-cache-max-size=<sessionCacheMaxSize>
                          Define the number of maximum sessions to cache.
--session-caching         Enable/disable the session caching
--uds                     File path for unix domain socket
```
