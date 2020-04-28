# Asherah Server - Java

## Running the server
The following makes use of the `JDBC` metastore implementation and assumes mysql is running on localhost and 
a preexisting Asherah database. See [metastore documentation](/docs/Metastore.md) for more.

> Development note: Requires JAVA 1.8 

```console
[user@machine java]$ mvn clean install
[user@machine java]$ java -jar <jar-path> --uds='/tmp/appencryption.sock' \
    --product-id=product \
    --service-id=service \
    --metastore-type=JDBC \
    --jdbc-url='jdbc:mysql://localhost/test?user=root&password=password' \
    --kms-type=static \
    --key-expiration-days 90 \
    --revoke-check-minutes 60
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using static KMS...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using JDBC-based metastore...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - key expiration days set to = 90 days
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - revoke check minutes set to = 60 minutes
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - server has started listening on /tmp/appencryption.sock
```

Arguments can also be supplied using environment variables

```bash
export ASHERAH_PRODUCT_NAME=product
export ASHERAH_SERVICE_NAME=service
export ASHERAH_EXPIRE_AFTER=90
export ASHERAH_CHECK_INTERVAL=60
export ASHERAH_METASTORE_MODE=jdbc
export ASHERAH_CONNECTION_STRING='jdbc:mysql://localhost/test?user=root&password=password'
export ASHERAH_KMS_MODE=static

mvn clean install
java -jar <jar-path> --uds='/tmp/appencryption.sock'
```

### Using the provided docker image
> **NOTE**: This docker image would not work as is with the [Amazon ECS example](../README.md#amazon-ecs).
> This is because the configuration options supported by the Java server slightly differ from those expected by the 
> alternative [Go server](../go).

```console
[user@machine java]$ mvn clean install
[user@machine java]$ docker build --build-arg JAR_FILE=<path-to-jar-file-with-dependencies> -f Dockerfile .
Sending build context to Docker daemon  47.37MB
Step 1/11 : FROM openjdk:8-jre-alpine
 ---> f7a292bbb70c
... snipped
Step 10/11 : USER aeljava
 ---> Running in 83998149b2f7
Removing intermediate container 83998149b2f7
 ---> 01d9203abe43
Step 11/11 : ENTRYPOINT ["java", "-Djna.nounpack=true", "-jar", "app.jar"]
 ---> Running in 9651fa614533
Removing intermediate container 9651fa614533
 ---> e9cb70abb481
Successfully built e9cb70abb481
[user@machine java]$ docker run -it e9cb70abb481 
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using static KMS...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - using in-memory metastore...
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - key expiration days set to = 90 days
[main] INFO com.godaddy.asherah.grpc.AppEncryptionConfig - revoke check minutes set to = 60 minutes
[main] INFO com.godaddy.asherah.grpc.AppEncryptionServer - server has started listening on /tmp/appencryption.sock
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
--product-id=<productId>
                          Specify the product id
--region-arn-tuples=<String=String>[,<String=String>...]
                          Comma separated list of <region>=<kms_arn> tuples.
                          Required for AWS KMS.
--revoke-check-minutes=<revokeCheckMinutes>
                          Sets the cache's TTL in minutes to revoke the keys in the cache
--service-id=<serviceId>   Specify the service id
--session-cache-expire-minutes=<sessionCacheExpireMinutes>
                          Evict the session from cache after some minutes.
--session-cache-max-size=<sessionCacheMaxSize>
                          Define the number of maximum sessions to cache.
--session-caching         Enable/disable the session caching
--uds                     File path for unix domain socket
```
