# Asherah Server - Java

## Running the server
The following makes use of the `JDBC-based` metastore implementation and assumes mysql is running on localhost and 
a preexisting Asherah database. See [metastore documentation](/docs/Metastore.md) for more.

```console
[user@machine java]$ mvn clean install
[user@machine java]$ java -jar <jar-path> --uds='/tmp/appencryption.sock' \
    --product-id=product, \
    --service-id=service, \
    --metastore-type=JDBC \
    --jdbc-url='jdbc:mysql://localhost/test?user=root&password=password', \
    --kms-type=static, \
    --key-expiration-days=90, \
    --revoke-check-minutes=60
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using static KMS...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using JDBC-based metastore...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - key expiration days set to = 90 days
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - revoke check minutes set to = 60 minutes
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - server has started listening on /tmp/appencryption.sock
```

Arguments can also be supplied using environment variables

```console
export ASHERAH_PRODUCT_NAME=product
export ASHERAH_SERVICE_NAME=service
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=60
export ASHERAH_METASTORE_MODE=memory
export ASHERAH_CONNECTION_STRING='jdbc:mysql://localhost/test?user=root&password=password'
export ASHERAH_KMS_MODE=static

[user@machine java]$ mvn clean install
[user@machine java]$ java -jar <jar-path> --uds='/tmp/appencryption.sock'
```

## Configuring the server
Configuration options are provided via command-line arguments or environment variables. Supported options are as
follows:

```console
--jdbc-url=<jdbcUrl>      JDBC URL to use for JDBC metastore. Required for JDBC metastore.
--key-expiration-days=<keyExpirationDays>
                          The number of days after which a key will expire
--kms-type=<kmsType>      Type of key management service to use.
                          Possible values: STATIC, AWS
--metastore-type=<metastoreType>
                          Type of metastore to use. 
                          Possible values: MEMORY, JDBC, DYNAMODB
--preferred-region=<preferredRegion>
                          Preferred region to use for KMS if using AWS KMS.
                          Required for AWS KMS.
--productId=<productId>
                          Specify the product id
--region-arn-tuples=<String=String>[,<String=String>...]
                          Comma separated list of <region>=<kms_arn> tuples.
                          Required for AWS KMS.
--revoke-check-minutes=<revokeCheckMinutes>
                          Sets the cache's TTL in minutes to revoke the keys in the cache
--serviceId=<serviceId>   Specify the service id
--session-cache-expire-minutes=<sessionCacheExpireMinutes>
                          Evict the session from cache after some minutes.
--session-cache-max-size=<sessionCacheMaxSize>
                          Define the number of maximum sessions to cache.
--session-caching         Enable/disable the session caching
--uds                     File path for unix domain socket
```
