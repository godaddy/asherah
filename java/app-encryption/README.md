# Asherah - Java
Application level envelope encryption SDK for Java with support for cloud-agnostic data storage and key management.

[![Version](https://img.shields.io/maven-central/v/com.godaddy.asherah/appencryption)](https://mvnrepository.com/artifact/com.godaddy.asherah/appencryption)

  * [Installation](#installation)
  * [Quick Start](#quick-start)
  * [How to Use Asherah](#how-to-use-asherah)
    * [Define the Metastore](#define-the-metastore)
    * [Define the Key Management Service](#define-the-key-management-service)
    * [Define the Crypto Policy](#define-the-crypto-policy)
      * [(Optional) Enable Session Caching](#optional-enable-session-caching)
    * [(Optional) Enable Metrics](#optional-enable-metrics)
    * [Build a Session Factory](#build-a-session-factory)
    * [Performing Cryptographic Operations](#performing-cryptographic-operations)
  * [Deployment Notes](#deployment-notes)
    * [Handling read\-only Docker containers](#handling-read-only-docker-containers)
  * [Development Notes](#development-notes)

## Installation

You can include Asherah in Java projects projects using [Maven](https://maven.apache.org/)

The Maven group ID is `com.godaddy.asherah`, and the artifact ID is `appencryption`.

You can specify the current release of Asherah as a project dependency using the following configuration:

```xml
<dependencies>
  <dependency>
    <groupId>com.godaddy.asherah</groupId>
    <artifactId>appencryption</artifactId>
    <version>0.1.1</version>
  </dependency>
</dependencies>
```

## Quick Start

```java
// Create a session factory. The builder steps used below are for testing only.
try (SessionFactory sessionFactory = SessionFactory.newBuilder("some_product", "some_service")
    .withInMemoryMetastore()
    .withNeverExpiredCryptoPolicy()
    .withStaticKeyManagementService("thisIsAStaticMasterKeyForTesting")
    .build()) {

  // Now create a cryptographic session for a partition.
  try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("some_partition")) {

    // Now encrypt some data
    String originalPayloadString = "mysupersecretpayload";
    byte[] dataRowRecordBytes = sessionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

    // Decrypt the data
    String decryptedPayloadString = new String(sessionBytes.decrypt(dataRowRecordBytes), StandardCharsets.UTF_8);
  }
}
```

A more extensive example is the [Reference Application](../../samples/java/reference-app), which will evolve along with the SDK.

## How to Use Asherah

Before you can start encrypting data, you need to define Asherah's required pluggable components. Below we show how to
build the various options for each component.

### Define the Metastore

Detailed information about the Metastore, including any provisioning steps, can be found [here](../../docs/Metastore.md).

#### RDBMS Metastore

Asherah can connect to a relational database by accepting a JDBC DataSource for connection handling.

```java
// Create / retrieve a DataSource from your connection pool
DataSource dataSource = ...;

// Build the JDBC Metastore
Metastore jdbcMetastore = JdbcMetastoreImpl.newBuilder(dataSource).build();
```

#### DynamoDB Metastore

```java
Metastore dynamoDbMetastore = DynamoDbMetastoreImpl.newBuilder().build();
```

#### In-memory Metastore (FOR TESTING ONLY)

```java
Metastore<JSONObject> metastore = new InMemoryMetastoreImpl<>();
```

### Define the Key Management Service

Detailed information about the Key Management Service can be found [here](../../docs/KeyManagementService.md).

#### AWS KMS

```java
// Create a map of region and ARN pairs that will all be used when encrypting a System Key
Map<String, String> regionMap = ImmutableMap.of("us-east-1", "arn_of_us-east-1",
    "us-east-2", "arn_of_us-east-2",
    ...);

// Build the Key Management Service using the region map and your preferred (usually current) region
KeyManagementService keyManagementService = AwsKeyManagementServiceImpl.newBuilder(regionMap, "us-east-1").build();
```

#### Static KMS (FOR TESTING ONLY)

```java
KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl("thisIsAStaticMasterKeyForTesting");
```

### Define the Crypto Policy

Detailed information about Crypto Policy can be found [here](../../docs/CryptoPolicy.md). The Crypto Policy's effect
on key caching is explained [here](../../docs/KeyCaching.md).


#### Basic Expiring Crypto Policy

```java
CryptoPolicy basicExpiringCryptoPolicy = BasicExpiringCryptoPolicy.newBuilder()
    .withKeyExpirationDays(90)
    .withRevokeCheckMinutes(60)
    .build();
```

#### (Optional) Enable Session Caching

Session caching is disabled by default. Enabling it is primarily useful if you are working with stateless workloads and the 
shared session can't be used by the calling app.

To enable session caching, simply use the optional builder step `withCanCacheSessions(true)` when building a crypto policy.

```java
CryptoPolicy basicExpiringCryptoPolicy = BasicExpiringCryptoPolicy.newBuilder()
    .withKeyExpirationDays(90)
    .withRevokeCheckMinutes(60)
    .withCanCacheSessions(true)    // Enable session cache
    .withSessionCacheMaxSize(200)    // Define the number of maximum sessions to cache
    .withSessionCacheExpireMinutes(5)    // Evict the session from cache after some minutes
    .build();
```

#### Never Expired Crypto Policy (FOR TESTING ONLY)

```java
CryptoPolicy neverExpiredCryptoPolicy = new NeverExpiredCryptoPolicy();
```

### (Optional) Enable Metrics

Asherah's Java implementation uses [Micrometer](http://micrometer.io/) for metrics, which are disabled by default. All metrics
generated by this SDK use the [global registry](https://micrometer.io/docs/concepts#_global_registry) and
use a prefix defined by `MetricsUtil.AEL_METRICS_PREFIX` (`ael` as of this writing). If metrics are left disabled,
we rely on Micrometer's [deny filtering](https://micrometer.io/docs/concepts#_deny_accept_meters).

To enable metrics generation, simply use the final optional builder step `withMetricsEnabled()` when building a session factory:

The following metrics are available:
- *ael.drr.decrypt:* Total time spent on all operations that were needed to decrypt.
- *ael.drr.encrypt:* Total time spent on all operations that were needed to encrypt.
- *ael.kms.aws.decrypt.\<region\>:* Time spent on decrypting the region-specific keys.
- *ael.kms.aws.decryptkey:* Total time spend in decrypting the key which would include the region-specific decrypt calls
in case of transient failures.
- *ael.kms.aws.encrypt.\<region\>:* Time spent on data key plain text encryption for each region.
- *ael.kms.aws.encryptkey:* Total time spent in encrypting the key which would include the region-specific generatedDataKey
and parallel encrypt calls.
- *ael.kms.aws.generatedatakey.\<region\>:* Time spent to generate the first data key which is then encrypted in remaining regions.
- *ael.metastore.jdbc.load:* Time spent to load a record from jdbc metastore.
- *ael.metastore.jdbc.loadlatest:* Time spent to get the latest record from jdbc metastore.
- *ael.metastore.jdbc.store:* Time spent to store a record into jdbc metastore.
- *ael.metastore.dynamodb.load:* Time spent to load a record from DynamoDB metastore.
- *ael.metastore.dynamodb.loadlatest:* Time spent to get the latest record from DynamoDB metastore.
- *ael.metastore.dynamodb.store:* Time spent to store a record into DynamoDB metastore.

### Build a Session Factory

A session factory can now be built using the components we defined above.

```java
SessionFactory sessionFactory = SessionFactory.newBuilder("some_product", "some_service")
  .withMetastore(metastore)
  .withCryptoPolicy(policy)
  .withKeyManagementService(keyManagementService)
  .withMetricsEnabled() // optional
  .build();
```

**NOTE:** We recommend that every service have its own session factory, preferably as a singleton instance within the service.
This will allow you to leverage caching and minimize resource usage. Always remember to close the session factory before exiting
the service to ensure that all resources held by the factory, including the cache, are disposed of properly.

### Performing Cryptographic Operations

Create a `Session` to be used for cryptographic operations.

```java
Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("some_user");
```

The different usage styles are explained below.

**NOTE:** Remember to close the session after all cryptographic operations to dispose of associated resources.

#### Plain Encrypt/Decrypt Style

This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is
completely up to the calling application for storage responsibility.

```java
String originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = sessionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

// decrypt the payload
String decryptedPayloadString = new String(sessionBytes.decrypt(dataRowRecordBytes), StandardCharsets.UTF_8);
```

#### Custom Persistence via Store/Load methods

Asherah supports a key-value/document storage model. A [Session](src/main/java/com/godaddy/asherah/appencryption/Session.java) 
can accept a [Persistence](src/main/java/com/godaddy/asherah/appencryption/persistence/Persistence.java)
implementation and hook into its `store` and `load` calls.

An example `HashMap`-backed `Persistence` implementation:

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

An example end-to-end use of the store and load calls:

```java
// Encrypts the payload, stores it in the dataPersistence and returns a look up key
String persistenceKey = sessionJson.store(originalPayload.toJsonObject(), dataPersistence);

// Uses the persistenceKey to look-up the payload in the dataPersistence, decrypts the payload if any and then returns it
Optional<JSONObject> payload = sessionJson.load(persistenceKey, dataPersistence);
```

## Deployment Notes

### Handling read-only Docker containers

SecureMemory currently uses [JNA](link) for native calls. The default behavior of JNA is to unpack the native libraries from
the jar to a temp folder, which can fail in a read-only container. The native library can instead be installed as a
part of the container. The general steps are:

1. Install the native JNA package
2. Specify `-Djna.nounpack=true` so the container never attempts to unpack bundled native libraries from the JNA jar.

The following are distro-specific notes that we know about:

* **Alpine**
  * The native package is `java-jna-native`.

* **Debian**
  * The native package, `libjna-jni`, needs to be installed from the Debian testing repo as the current base image
  is not compatible with AEL's JNA version. The example Debian Dockerfile listed below adds the testing repo before
  installing this package and is then removed.
  * Add the property `-Djna.boot.library.name=jnidispatch.system` in java exec as Debian package contains an extra
  ".system" in the library name.

* **Ubuntu**
  * The native package is `libjna-jni`.
  * Add the property `-Djna.boot.library.name=jnidispatch.system` in java exec as Ubuntu package contains an extra
  ".system" in the library name.
  * If using the `adoptopenjdk/openjdk` base image, we need to add additional directories in the default library
  path using `-Djna.boot.library.path=/usr/lib/x86_64-linux-gnu/jni/`

Our [test app repo's](../../tests/java/test-app/) Dockerfiles can be used for reference:
[Alpine](../../tests/java/test-app/images/alpine/Dockerfile), [Debian](../../tests/java/test-app/images/debian/Dockerfile)
and [Ubuntu](../../tests/java/test-app/images/ubuntu/Dockerfile) (uses [AdoptOpenJDK](https://adoptopenjdk.net/))

## Development Notes

Some unit tests will use the AWS SDK, If you don’t already have a local
[AWS credentials file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html),
create a *dummy* file called **`~/.aws/credentials`** with the below contents:

```
[default]
aws_access_key_id = foobar
aws_secret_access_key = barfoo
```

Alternately, you can set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

