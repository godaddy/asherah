# ASHERAH - Java
Application level envelope encryption SDK for Java with support for cloud-agnostic data storage and key management.

  * [Basic Example](#basic-example)
  * [SDK Details](#sdk-details)
  * [Metrics](#metrics)
  * [Deployment Notes](#deployment-notes)
    * [Handling read\-only Docker containers](#handling-read-only-docker-containers)
    * [Setting rlimits](#setting-rlimits)
  * [SDK Development Notes](#sdk-development-notes)
    * [Running Tests Locally via Docker Image](#running-tests-locally-via-docker-image)

## Basic Example

Asherah generally uses the **builder pattern** to define objects.

``` java

// First build a basic Crypto Policy that expires
// keys after 90 days and has a cache TTL of 60 minutes
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
    .newBuilder()
    .withKeyExpirationDays(90)
    .withRevokeCheckMinutes(60)
    .build();

// Create a session factory for this app. Normally this would be done upon app startup and the
// same factory would be used anytime a new session is needed for a partition (e.g., shopper).
// We've split it out into multiple try blocks to underscore this point.
try (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
    .newBuilder("productId", "sample_code")
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
You can also review the [Reference Application](../../samples/java/reference-app), which will evolve along with the 
SDK and show more detailed usage.

## SDK Details

Asherah supports a key-value/document storage model. An [AppEncryption](src/main/java/com/godaddy/asherah/appencryption/AppEncryption.java) instance can accept a [Persistence](src/main/java/com/godaddy/asherah/appencryption/persistence/Persistence.java) implementation
and hooks into its `load` and `store` calls. This can be seen in the interface definition:

 ```java
public interface AppEncryption<P, D> extends SafeAutoCloseable {

  /**
   * Uses a persistence key to load a Data Row Record from the provided data persistence store, if any, and returns the
   * decrypted payload.
   * @param persistenceKey
   * @param dataPersistence
   * @return The decrypted payload, if found in persistence
   */
  default Optional<P> load(final String persistenceKey, final Persistence<D> dataPersistence) {
    return dataPersistence.load(persistenceKey)
        .map(this::decrypt);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store, and returns its
   * associated persistence key for future lookups.
   * @param payload
   * @param dataPersistence
   * @return The persistence key associated with the stored Data Row Record
   */
  default String store(final P payload, final Persistence<D> dataPersistence) {
    D dataRowRecord = encrypt(payload);

    return dataPersistence.store(dataRowRecord);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store with given key
   * @param key
   * @param payload
   * @param dataPersistence
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
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, 
and it is completely up to the calling application for storage responsibility.

```java
String originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = appEncryptionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

// decrypt the payload
String decryptedPayloadString = new String(appEncryptionBytes.decrypt(newBytes), StandardCharsets.UTF_8);
```

Further details on the working of SDK are defined [here](https://github.com/godaddy/asherah/tree/master#further-reading).

### Metrics
Asherah uses [Micrometer](http://micrometer.io/) for metrics, which are disabled by default. All metrics 
generated by this SDK use the [global registry](https://micrometer.io/docs/concepts#_global_registry) and 
use a prefix defined by `MetricsUtil.AEL_METRICS_PREFIX` (`ael` as of this writing). If metrics are left disabled, 
we rely on Micrometer's [deny filtering](https://micrometer.io/docs/concepts#_deny_accept_meters).

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

The default behavior of JNA is to unpack the native libraries from the jar to a temp folder, which can fail in a 
read-only container. The native library can instead be installed as a part of the container. The general steps are:

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

### Setting rlimits

Asherah uses the [SecureMemory Library](../secure-memory) for protecting cached keys. Among other things, 
this SDK uses system calls in order to lock the memory used for caching to prevent access to that memory 
from other processes. The amount of memory a user can lock can be limited by the OS and we have seen different 
default values across the different distributions and docker images. If the value that is set is not sufficient 
for the amount of caching needed, the SDK will throw an "Insufficient Memory" exception. There are different 
ways to resolve the issue:

#### System-wide `limits.conf`

On Linux servers the `/etc/security/limits.conf` file allows for the configuration of system-wide and user-specific 
memory locking limits. While this can be specified differently for each user we will set this to unlimited for 
all users on our test servers:

``` console
# <User>     <soft/hard/both>     <item>        <value>
*                 -               memlock      unlimited
```

**Note:** We've seen that in some cases (EKS worker nodes for us), `systemd` can override `rlimits` for service 
under its management so that the `limits.conf` changes do not have any affect. See below in the Kubernetes section 
how this was handled with a `systemd` override configuration.

#### Running Docker containers

When running docker containers (such as our testing framework) the `--ulimit` option can be used set memory locking 
limits:

``` console
docker run -it --ulimit memlock=-1:-1  [...]
```

#### AWS EKS

(This may apply to Kubernetes in general, as well, but we only experienced it using EKS.)

The EKS worker nodes that we've used have a built-in default of 64k as the amount of memory that a user can lock. 
This is too small for our testing framework's normal run. As noted above, setting new values in 
`/etc/security/limits.conf` did not affect the docker service as `systemd` appears to have its own override. 
Our solution was to modify the docker service's memlock limit using systemd's configuration override mechanism. 
We accomplished this by adding a `CustomUserData` parameter to our EKS sceptre configuration:

``` console
CustomUserData: "mkdir -p /etc/systemd/system/docker.service.d && echo '[Service]\nLimitMEMLOCK=infinity\n' > /etc/systemd/system/docker.service.d/override.conf && /bin/systemctl daemon-reload && /bin/systemctl restart docker.service"
```

This creates an override configuration with the following content:

``` ini
[Service]
LimitMEMLOCK=infinity
```

## SDK Development Notes

### Running Tests Locally via Docker Image
Below is an example of how to run tests locally using a Docker image. This one is using the build image used 
for Jenkins build/deployment, but could be replaced with other images for different targeted platforms.  
**Note:** This is run from your project directory.

**TODO:** This may no longer work locally after introducing docker in docker. Likely due to local gid mapping w/ docker
 group being used in build image.

```console
[user@machine AsherahJava]$ docker build images/build
...
Successfully built <generated_image_id>
[user@machine AsherahJava]$ docker run -it --rm -v $HOME/.m2:/home/jenkins/.m2 -v /var/run/docker.sock:/var/run/docker.sock --group-add docker -v "$PWD":/usr/app/src -w /usr/app/src --ulimit memlock=-1:-1 <generated_image_id> mvn clean install
...
```

**Note**: The above build is known to work on macOS due to how the bind mounts map UIDs, too. On Linux systems 
you will likely need to add the optional build arguments:

``` console
[user@machine AsherahJava]$ docker build --build-arg UID=$(id -u) --build-arg GID=$(id -g) images/build
```

This will create the container's user with your UID so that it has full access to the `.m2` directory.
