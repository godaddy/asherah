# Internals

## Common APIs

Below are the primary public-facing interfaces of Asherah.

**NOTE:** The interfaces below are from the Java implementation of the SDK.

**Primary SDK Interface**

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

  // When using the load/store style, this defines the callbacks used to interact with Data Row Records.
  interface Persistence<T> {
    Optional<T> load(String key);
    String store(T value);
    void store(String key, T value);
    String generateKey(T value);
  }
 
   // Create a session factory using the step builder pattern. 
   AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
      .newBuilder("myservice", "sample_code")
      .withMemoryPersistence() 
      .withNeverExpiredCryptoPolicy()
      .withStaticKeyManagementService("secretmasterkey!")
      .build());

    // Use the factory to get an AppEncryption instance
    AppEncryption<byte[], byte[]> appEncryptionBytes = appEncryptionSessionFactory.getAppEncryptionBytes("partitionId");
```

**[Cryptopolicy](CryptoPolicy.md)**

```java
  // Used to configure various behaviors of the internal algorithm
  interface CryptoPolicy {
    enum KeyRotationStrategy {
        INLINE, // note this is the only one currenly supported/implemented
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

**[Metastore](Metastore.md)**

```java

  // Defines the backing metastore
  interface MetastorePersistence<V> {
    Optional<V> load(String keyId, Instant created);
    Optional<V> loadLatestValue(String keyId);

    boolean store(String keyId, Instant created, V value);
}
```

**[Key Management Service](KeyManagementService.md)**

```java

  // Defines the root KMS
  interface KeyManagementService {
    byte[] encryptKey(CryptoKey key);
    CryptoKey decryptKey(byte[] keyCipherText, Instant keyCreated, boolean revoked);

    <T> T withDecryptedKey(byte[] keyCipherText, Instant keyCreated, boolean revoked,
                           BiFunction<CryptoKey, Instant, T> actionWithDecryptedKey);
}
```

## Potential future enhancements:

Add support for multiple cipher suites.

```java
  interface CryptoPolicy {
    ...

    enum CipherSuite {
        AES-256-GCM,
        ...
    };
    CipherSuite getCipherSuite();
    int getNonceSizeBits();
}
```
