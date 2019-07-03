# Common APIs and Algorithm Internals

## Common APIs

Below are the primary public-facing interfaces of Asherah.

**NOTE:** The interfaces below are from the Java implementation of the SDK, which also serves as the reference 
implementation

### Primary SDK Interfaces

The below interfaces implement the session factory using the step builder pattern.

```java
class AppEncryptionSessionFactory {
  static MetastoreStep newBuilder(String productId, String systemId);
  
  AppEncryption<JSONObject, byte[]> getAppEncryptionJson(String partitionId);
  AppEncryption<byte[], byte[]> getAppEncryptionBytes(String partitionId);
  AppEncryption<JSONObject, JSONObject> getAppEncryptionJsonAsJson(String partitionId);
  AppEncryption<byte[], JSONObject> getAppEncryptionBytesAsJson(String partitionId);
  
  void close();
}
 
interface MetastoreStep {
  CryptoPolicyStep withMetastorePersistence(MetastorePersistence<JSONObject> persistence);
}

interface CryptoPolicyStep {
  KeyManagementServiceStep withCryptoPolicy(CryptoPolicy policy);
}

interface KeyManagementServiceStep {
  BuildStep withKeyManagementService(KeyManagementService keyManagementService);
}


interface BuildStep {
  BuildStep withMetricsEnabled();
  // Additional optional steps can be added here
  
  AppEncryptionSessionFactory build();
}
```

Cryptographic operations are performed using the methods provided in the `AppEncryption` interface.

```java
// <P> The payload type being encrypted
// <D> The Data Row Record type
interface AppEncryption<P, D> {
  P decrypt(D dataRowRecord);
  D encrypt(P payload);

  Optional<P> load(String persistenceKey, Persistence<D> dataPersistence);
  String store(P payload, Persistence<D> dataPersistence);
  void store(String key, P payload, Persistence<D> dataPersistence);

  void close();
}
```
  
For the [store/load](../README.md#store--load) usage model, we also need to implement the `Persistence` interface

```java
// When using the store/load style, this defines the callbacks used to interact with Data Row Records.
interface Persistence<T> {
  Optional<T> load(String key);
  String store(T value);
  void store(String key, T value);
  String generateKey(T value);
}
```

### Crypto Policy

```java
  // Used to configure various behaviors of the internal algorithm
  interface CryptoPolicy {
    enum KeyRotationStrategy {
        INLINE, // This is the only one currently supported/implemented
        QUEUED
    };
    KeyRotationStrategy keyRotationStrategy();

    boolean isKeyExpired(Instant keyCreationDate);
    long getRevokeCheckPeriodMillis();

    boolean canCacheSystemKeys();
    boolean canCacheIntermediateKeys();

    boolean notifyExpiredIntermediateKeyOnRead();
    boolean notifyExpiredSystemKeyOnRead();
}
```

Detailed information about the CryptoPolicy can be found [here](CryptoPolicy.md) 

### Metastore

```java
  // Defines the backing metastore
  interface MetastorePersistence<V> {
    Optional<V> load(String keyId, Instant created);
    Optional<V> loadLatestValue(String keyId);

    boolean store(String keyId, Instant created, V value);
}
```

Detailed information about the Metastore can be found [here](Metastore.md) 


### Key Management Service

```java
  // Defines the root KMS
  interface KeyManagementService {
    byte[] encryptKey(CryptoKey key);
    CryptoKey decryptKey(byte[] keyCipherText, Instant keyCreated, boolean revoked);

    <T> T withDecryptedKey(byte[] keyCipherText, Instant keyCreated, boolean revoked,
                           BiFunction<CryptoKey, Instant, T> actionWithDecryptedKey);
}
```

Detailed information about the  Key Management Service can be found [here](KeyManagementService.md) 

