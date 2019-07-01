# App Encryption - Java
Application level envelope encryption SDK for Java with support for cloud-agnostic data storage and key management.

Table of Contents
=================

  * [Basic Example](#basic-example)
  * [SDK Details](#sdk-details)
    * [Usage Styles](#usage-styles)
      * [Custom Persistence via Load/Store methods](#custom-persistence-via-loadstore-methods)
      * [Plain Encrypt/Decrypt style](#plain-encryptdecrypt-style)
    * [Metastore](#metastore)
      * [JDBC Metastore](#jdbc-metastore)
        * [Row Data Size Estimates](#row-data-size-estimates)
      * [DynamoDB Metastore](#dynamodb-metastore)
        * [Item Data Size Estimates](#item-data-size-estimates)
      * [In\-memory Metastore (FOR TESTING ONLY)](#in-memory-metastore-for-testing-only)
    * [Key Management Service](#key-management-service)
      * [AWS KMS](#aws-kms)
      * [Static KMS (FOR TESTING ONLY)](#static-kms-for-testing-only)
    * [Crypto Policy](#crypto-policy)
      * [Basic Expiring Crypto Policy](#basic-expiring-crypto-policy)
      * [Never Expiring Crypto Policy (FOR TESTING ONLY)](#never-expiring-crypto-policy-for-testing-only)
    * [Key Caching](#key-caching)
    * [Metrics](#metrics)
  * [Deployment Notes](#deployment-notes)
    * [Handling read\-only Docker containers](#handling-read-only-docker-containers)
    * [Setting rlimits](#setting-rlimits)
    * [Revoking keys](#revoking-keys)
  * [SDK Development Notes](#sdk-development-notes)
    * [Running Tests Locally via Docker Image](#running-tests-locally-via-docker-image)

## Basic Example

The App Encryption SDK generally uses the **builder pattern** to define objects.

```java
// First build a basic Crypto Policy that expires
// keys after 90 days and has a cache TTL of 60 minutes
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy.newBuilder()
    .withKeyExpirationDays(90)
    .withRevokeCheckMinutes(60)
    .build();

// Create a session factory for this app. Normally this would be done upon app startup and the
// same factory would be used anytime a new session is needed for a partition (e.g., shopper).
// We've split it out into multiple try blocks to underscore this point.
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.newBuilder("productId", "sample_code")
    .withMemoryPersistence() // in-memory metastore persistence only
    .withCryptoPolicy(cryptoPolicy)
    .withStaticKeyManagementService("secretmasterkey!") // hard-coded/static master key
    .build()) {

  // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
  // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
  try (AppEncryption<byte[], byte[]> appEncryptionBytes = appEncryptionSessionFactory.getAppEncryptionBytes("shopper123")) {

    // Now encrypt some data
    String originalPayloadString = "mysupersecretpayload";

    byte[] dataRowRecordBytes = appEncryptionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    String dataRowString = Base64.getEncoder().encodeToString(dataRowRecordBytes);

    System.out.println("dataRowRecordBytes = " + dataRowString);

    byte[] newBytes = Base64.getDecoder().decode(dataRowString);

    // Ensure we can decrypt the data, too
    String decryptedPayloadString = new String(appEncryptionBytes.decrypt(newBytes), StandardCharsets.UTF_8);

    System.out.println("decryptedPayloadString = " + decryptedPayloadString + ", matches = "
                       + originalPayloadString.equals(decryptedPayloadString));
  }
}
```
You can also review the [Reference Application](../../samples/java/reference-app), which will evolve along with the sdk
 and show more detailed usage.

## SDK Details

### Usage Styles

#### Custom Persistence via Load/Store methods
The SDK supports a key-value/document storage model. An [AppEncryption](src/main/java/com/godaddy/asherah/appencryption/AppEncryption.java) instance can accept a [Persistence](src/main/java/com/godaddy/asherah/appencryption/persistence/Persistence.java) implementation
and hooks into its `load` and `store` calls. This can be seen in the interface definition:

 ```java
public interface AppEncryption<P, D> extends SafeAutoCloseable {

  /**
   * Uses a persistence key to load a Data Row Record from the provided data persistence store, if any, and returns the
   * decrypted payload.
   * @param persistenceKey the key to lookup in the data persistence store
   * @param dataPersistence the data persistence store to use
   * @return The decrypted payload, if found in persistence
   */
  default Optional<P> load(final String persistenceKey, final Persistence<D> dataPersistence) {
    return dataPersistence.load(persistenceKey)
        .map(this::decrypt);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store, and returns its
   * associated persistence key for future lookups.
   * @param payload the payload to encrypt and store
   * @param dataPersistence the data persistence store to use
   * @return The persistence key associated with the stored Data Row Record
   */
  default String store(final P payload, final Persistence<D> dataPersistence) {
    D dataRowRecord = encrypt(payload);

    return dataPersistence.store(dataRowRecord);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store with given key
   * @param key the key to associate the Data Row Record with
   * @param payload the payload to encrypt and store
   * @param dataPersistence the data persistence store to use
   */
  default void store(final String key, final P payload, final Persistence<D> dataPersistence) {
    D dataRowRecord = encrypt(payload);

    dataPersistence.store(key, dataRowRecord);
  }
```

Example `HashMap`-backed `Persistence` implementation:

```java
Persistence dataPersistence = new Persistence<JSONObject>() {

  Map<String, JSONObject> mapPersistence = new HashMap<>();

  @Override
  public Optional<JSONObject> load(String key) {
    return Optional.ofNullable(mapPersistence.get(key));
  }

  @Override
  public void store(String key, JSONObject value) {
    mapPersistence.put(key, value);
  }
};
```

Putting it all together, an example end-to-end use of the store and load calls:

```java
// Encrypts the payload, stores it in the dataPersistence and returns a look up key
String persistenceKey = appEncryptionJson.store(originalPayload.toJsonObject(), dataPersistence);

// Uses the persistenceKey to look-up the payload in the dataPersistence, decrypts the payload if any and then returns it
Optional<JSONObject> payload = appEncryptionJson.load(persistenceKey, dataPersistence);
```

#### Plain Encrypt/Decrypt Style
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is completely up to the calling application for storage responsibility.

```java
String originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = appEncryptionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

// decrypt the payload
String decryptedPayloadString = new String(appEncryptionBytes.decrypt(newBytes), StandardCharsets.UTF_8);
```


### Metastore
The SDK handles the storage of Intermediate and System Keys in its "Metastore" which can either be a completely separate datastore from the application's, or simply a table within an application's existing database.

#### JDBC Metastore
To use the JDBC-compliant Metastore included with the SDK, the following table should be created:

```sql
CREATE TABLE encryption_key (
  id             VARCHAR(255) NOT NULL,
  created        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  key_record     TEXT         NOT NULL,
  PRIMARY KEY (id, created),
  INDEX (created)
);
```
**NOTE**: The above schema is known to work properly with MySQL. Adjust the schema data types for other database vendors accordingly. As we officially integrate with teams using other database vendors, we will add more vendor-specific schemas as needed.

The JDBC Metastore follows the same builder pattern as the SDK and supports the use of a standard JDBC DataSource for connection handling so that any JDBC-compliant connection pool can be used:

```java
// Create / retrieve a DataSource from your connection pool
DataSource dataSource = ...;

// Build the JDBC Metastore
MetastorePersistence jdbcMetastorePersistence = JdbcMetastorePersistenceImpl.newBuilder(dataSource).build();

// Use the Metastore for the session factory
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.newBuilder("productId", "reference_app")
     .withMetastorePersistence(jdbcMetastorePersistence)
     .withCryptoPolicy(policy)
     .withKeyManagementService(keyManagementService)
     .build()) {

    // ...
}
```

##### Row Data Size Estimates
The estimates provided are based on examples using product id, system id, and partition id with lengths of 11, 13, and 10 bytes respectively.  

The row data size estimates were calculated by using the below query against actual data. Note this **does not** account for index size.

```sql
-- varchar 255 has 1 byte overhead, timestamp w/ no fractional seconds uses 4 bytes, and text has 2 bytes overhead
select id, created, (1 + octet_length(id) + 4 + 2 + octet_length(key_record)) as row_data_size from encryption_key;
```

**IntermediateKey**: 253 bytes  
**SystemKey**: 1227 bytes (assumes AWS KMS with 2 regions. each additional region adds ~494 bytes)


#### DynamoDB Metastore
To use the DynamoDB Metastore included with the SDK, the following table should be created:

```console
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

The DynamoDB Metastore follows the same builder pattern as the SDK:

```java
// Build the DynamoDB Metastore.
MetastorePersistence dynamoDbMetastorePersistence = DynamoDbMetastorePersistenceImpl.newBuilder().build();

// Use the Metastore for the session factory
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.newBuilder("productId", "reference_app")
     .withMetastorePersistence(dynamoDbMetastorePersistence)
     .withCryptoPolicy(policy)
     .withKeyManagementService(keyManagementService)
     .build()) {

    // ...
}
```

##### Item Data Size Estimates
The estimates provided are based on examples using product id, system id, and partition id with lengths of 11, 13, and 10 bytes respectively.  

The item data size estimates were calculated by using https://zaccharles.github.io/dynamodb-calculator/ (client-side calculation, no network calls observed).

**IntermediateKey**: 240 bytes  
**SystemKey**: 1227 bytes (assumes AWS KMS with 2 regions. each additional region adds ~494 bytes)  


#### In-memory Metastore (FOR TESTING ONLY)
The SDK also supports an in-memory metastore persistence model but that ***should only be used for testing purposes***.

### Key Management Service
The SDK requires a Key Management Service (KMS) to generate the top level key and to encrypt the System Keys. AWS KMS
 support is provided by the SDK, but an interface exists for applications to use another provider as needed.

#### AWS KMS
The KMS service of AWS provides the top level key. The SDK supports multiple regions and is configured using the familiar builder pattern:

```java
// Create a map of region and arn that will all be used when creating a System Key
Map<String, String> regionMap = ImmutableMap.of("us-east-1", "arn_of_us-east-1",
    "us-east-2", "arn_of_us-east-2",
    ...);

// Build the Key Management Service using the region map and your preferred (usually current) region
AWSKeyManagementServiceImpl keyManagementService = AWSKeyManagementServiceImpl.newBuilder(regionMap, "us-east-1").build();

// Provide the above keyManagementService to the session factory builder
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.newBuilder("productId", "reference_app")
     .withMetastorePersistence(metastorePersistence)
     .withCryptoPolicy(policy)
     .withKeyManagementService(keyManagementService)
     .build()) {

    // ...
}
```

We store a key encryption key for each region, of which any can be used to decrypt the encryption key used on the System Key. The configured preferred region is the first to be attempted for decryption. The meta information is stored internally in the following format:

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

NOTE: In case of a local region KMS failure, expect higher latency as a different region's KMS ARN will be used to decrypt the System Key. Keep in mind this should be rare since System Keys should be cached to further reduce likelihood of this.

#### Static KMS (FOR TESTING ONLY)
The SDK also supports a static KMS but it ***should never be used in production***.

### Crypto Policy
A crypto policy dictates the various behaviors of the SDK and can be configured with several options. The options available are:
- *canCacheSystemKeys*: enables/disables caching of System Keys
- *canCacheIntermediateKeys*: enables/disables caching of Intermediate Keys
- *getRevokeCheckPeriodMillis*: the time period to revoke keys present in cache
- *isKeyExpired*: defines if a key is expired
- *keyRotationStrategy*: enumeration of INLINE/QUEUED key rotation strategy.
- *notifyExpiredSystemKeyOnRead*: enables/disables notifications for expired System Key.
- *notifyExpiredIntermediateKeyOnRead*:  enables/disables notifications for expired Intermediate Key.

#### Basic Expiring Crypto Policy
This policy has been used in the example usage and it has some pre-configured defaults which should work for most of the cases. The 2 required behaviors for this policy are:
- *keyExpirationDays:* The number of days after which a key will expire. This is tied to the `isKeyExpired` interface method. If a key is expired, the old key is used to decrypt the data. Depending on the crypto policy configuration it is then rotated inline on the next write or queued for renewal (not implemented as of this writing).
- *revokeCheckMinutes:* Keys are stored in cache to prevent having to make a lot of calls to the external datastores for fetching and decrypting keys. This configuration sets the cache's TTL and is needed in case we chose to revoke the keys in the cache. This is tied to the `getRevokeCheckPeriodMillis` interface method.

#### Never Expiring Crypto Policy (FOR TESTING ONLY)
This policy supports keys that never expire nor ever removed from the cache. This ***should never be used in the production environment***.

### Key Caching

The SDK allows for the caching of the Intermediate and System Keys which is configured through the Crypto Policy explained above. Since keys can be "revoked" before the key's expiration period, the status of a cached key is periodically retrieved from the Metastore. This "Revoke Check" period is configurable through the Crypto Policy's `revokeCheckPeriodMillis`.  The Basic Crypto Policy provided by the SDK simplifies this to the granularity of a minute with `revokeCheckMinutes()` in the builder.

Due to the hierarchical nature of the System and Intermediate Keys, the scope of the caches are different. For System Keys, which can be thought of as product or application specific keys, their lifetime is that of the [AppEncryptionSessionFactory](src/main/java/com/godaddy/asherah/appencryption/AppEncryptionSessionFactory.java) -- the object that encryption sessions for partitions are created from.  This allows for the System Key cache to be shared across partitions so that each encryption session isn't caching another copy of the same System Key.

The cache for Intermediate Keys is, of course, scoped to the current partition's [AppEncryption](src/main/java/com/godaddy/asherah/appencryption/AppEncryption.java) session since there is no reason to share Intermediate Key caches across sessions.

For a real-world example, consider an API request made from a web application with a logged-in user. When the request is received an encryption session will be created from the session factory with that user as the defining partition. The first attempt at accessing encrypted data for that user will retrieve the Intermediate Key used for that data row's key encryption key. That Intermediate Key will be decrypted by the System Key which would most likely be retrieved from the cache that is shared across other encryption sessions.  The Intermediate Key would then be decrypted and cached for the lifetime of that encryption session, which is more than likely the lifetime of that API request.

### Metrics
The SDK uses [Micrometer](http://micrometer.io/) for metrics, which are disabled by default. All metrics generated by this SDK use the [global registry](https://micrometer.io/docs/concepts#_global_registry) and use a prefix defined by `MetricsUtil.AEL_METRICS_PREFIX` (`ael` as of this writing). If metrics are left disabled, we rely on Micrometer's [deny filtering](https://micrometer.io/docs/concepts#_deny_accept_meters).

To enable metrics generation, simply use the final optional builder step when creating an `AppEncryptionSessionFactory`:

```java
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.newBuilder("productId", "reference_app")
     .withMetastorePersistence(metastorePersistence)
     .withCryptoPolicy(policy)
     .withKeyManagementService(keyManagementService)
     .withMetricsEnabled()
     .build()) {

    // ...
}
```

The following metrics are available.
- *ael.drr.decrypt:* Total time spent on all operations that were needed to decrypt.
- *ael.drr.encrypt:* Total time spent on all operations that were needed to encrypt.
- *ael.kms.aws.decrypt.\<region\>:* Time spent on decrypting the region-specific keys.
- *ael.kms.aws.decryptkey:* Total time spend in decrypting the key which would include the region-specific decrypt calls in case of transient failures.
- *ael.kms.aws.encrypt.\<region\>:* Time spent on data key plain text encryption for each region.
- *ael.kms.aws.encryptkey:* Total time spent in encrypting the key which would include the region-specific generatedDataKey and parallel encrypt calls.
- *ael.kms.aws.generatedatakey.\<region\>:* Time spent to generate the first data key which is then encrypted in remaining regions.
- *ael.metastore.jdbc.load:* Time spent to load a record from jdbc metastore.
- *ael.metastore.jdbc.loadlatest:* Time spent to get the latest record from jdbc metastore.
- *ael.metastore.jdbc.store:* Time spent to store a record into jdbc metastore.
- *ael.metastore.dynamodb.load:* Time spent to load a record from DynamoDB metastore.
- *ael.metastore.dynamodb.loadlatest:* Time spent to get the latest record from DynamoDB metastore.
- *ael.metastore.dynamodb.store:* Time spent to store a record into DynamoDB metastore.

## Deployment Notes

### Handling read-only Docker containers

The default behavior of JNA is to unpack the native libraries from the jar to a temp folder, which can fail in a read-only container. The native library can instead be installed as a part of the container. The general steps are:

1. Install the native JNA package
2. Specify `-Djna.nounpack=true` so the container never attempts to unpack bundled native libraries from the JNA jar.

The following are distro-specific notes that we know about:

* **Alpine**
  * The native package is `java-jna-native`.

* **Debian**
  * The native package, `libjna-jni`, needs to be installed from the Debian testing repo as the current base image is
   not compatible with Asherah's JNA version. The example Debian Dockerfile listed below adds the testing repo before 
   installing this package and is then removed.
  * Add the property `-Djna.boot.library.name=jnidispatch.system` in java exec as Debian package contains an extra ".system" in the library name.

* **Ubuntu**
  * The native package is `libjna-jni`.
  * Add the property `-Djna.boot.library.name=jnidispatch.system` in java exec as Ubuntu package contains an extra ".system" in the library name.
  * If using the `adoptopenjdk/openjdk` base image, we need to add additional directories in the default library path using `-Djna.boot.library.path=/usr/lib/x86_64-linux-gnu/jni/`

Our [test app repo's](../path/to/testapp/) Dockerfiles can be used for reference: [Alpine](../path/to/testapp/images/alpine/Dockerfile), [Debian](../path/to/testapp/images/debian/Dockerfile) and [Ubuntu](../path/to/testapp/images/ubuntu/Dockerfile) (uses [AdoptOpenJDK](https://adoptopenjdk.net/))

### Setting rlimits

The SDK uses the [SecureMemory Library](../secure-memory) for protecting cached keys. Among other things, this library uses system calls in order to lock the memory used for caching to prevent access to that memory from other processes.  The amount of memory a user can lock can be limited by the OS and we have seen different default values across the different distributions and docker images.  If the value that is set is not sufficient for the amount of caching needed the library will throw an "Insufficient Memory" exception.  There are different ways to resolve the issue:

#### System-wide `limits.conf`

On Linux servers the `/etc/security/limits.conf` file allows for the configuration of system-wide and user-specific memory locking limits. While this can be specified differently for each user we will set this to unlimited for all users on our test servers:

``` console
# <User>     <soft/hard/both>     <item>        <value>
*                 -               memlock      unlimited
```

Note: we've seen that in some cases (EKS worker nodes for us), systemd can override rlimits for service under its management so that the `limits.conf` changes do not have any affect.  See below in the Kubernetes section how this was handled with a systemd override configuration.

#### Running Docker containers

When running docker containers (such as our testing framework) the --ulimit option can be used set memory locking limits:

``` console
docker run -it --ulimit memlock=-1:-1  [...]
```

#### AWS EKS

(This may apply to Kubernetes in general, as well, but we only experienced it using EKS.)

The EKS worker nodes that we've used have a built-in default of 64k as the amount of memory that a user can lock. This is too small for our testing framework's normal run. As noted above, setting new values in `/etc/security/limits.conf` did not affect the docker service as systemd appears to have its own override. Our solution was to modify the docker service's memlock limit using systemd's configuration override mechanism. We accomplished this by adding a `CustomUserData` parameter to our EKS sceptre configuration:

``` console
CustomUserData: "mkdir -p /etc/systemd/system/docker.service.d && echo '[Service]\nLimitMEMLOCK=infinity\n' > /etc/systemd/system/docker.service.d/override.conf && /bin/systemctl daemon-reload && /bin/systemctl restart docker.service"
```

This creates an override configuration with the following content:

``` ini
[Service]
LimitMEMLOCK=infinity
```

### Revoking keys

If for any reason there is a need to accelerate the rotation of a key that's been used by the SDK (e.g. suspected 
compromise of keys) there is support for marking a key as "revoked".

We have created helper python scripts for the below metastore implementations.  See their usage message for details on how to run them:
* [JDBC-based Metastore (MySQL)](scripts/revoke_keys_mysql.py)
* [DynamoDB Metastore](scripts/revoke_keys_dynamodb.py) (WARNING: Bulk operation uses Scan API, which reads the **entire** table. Avoid if possible, e.g. revoke individual system keys)

## SDK Development Notes

Some unit tests will use the AWS SDK, If you don’t already have a local [AWS credentials file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html), 
create a *dummy* file called **`~/.aws/credentials`** with the below contents:

```
[default]
aws_access_key_id = foobar
aws_secret_access_key = barfoo
```

Alternately, you can set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

### Running Tests Locally via Docker Image
Below is an example of how to run tests locally using a Docker image. This one is using the build image used for Jenkins build/deployment, but could be replaced with other images for different targeted platforms. Note this is run from your project directory.

TODO This may no longer work locally after introducing docker in docker. Likely due to local gid mapping w/ docker group being used in build image.

```console
[user@machine AppEncryptionJava]$ docker build images/build
...
Successfully built <generated_image_id>
[user@machine AppEncryptionJava]$ docker run -it --rm -v $HOME/.m2:/home/jenkins/.m2 -v /var/run/docker.sock:/var/run/docker.sock --group-add docker -v "$PWD":/usr/app/src -w /usr/app/src --ulimit memlock=-1:-1 <generated_image_id> mvn clean install
...
```

*Note*: The above build is known to work on macOS due to how the bind mounts map UIDs, too. On Linux systems you will likely need to add the optional build arguments:

``` console
[user@machine AppEncryptionJava]$ docker build --build-arg UID=$(id -u) --build-arg GID=$(id -g) images/build
```

This will create the container's user with your UID so that it has full access to the .m2 directory.

---
The table of contents was generated using [gh-md-toc](https://github.com/ekalinin/github-markdown-toc).
Usage: `./gh-md-toc <README.md>`
