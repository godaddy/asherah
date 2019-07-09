# Common APIs and Algorithm Internals

## Directory Structure

Language-specific best practices will apply to the full package name, but we'll use the Java reference implementation
for an example of how the code should be structured:

* root (`com.godaddy.asherah.appencryption`)
    * Interfaces and types a user interacts with during primary cryptographic operations.
* envelope (`com.godaddy.asherah.appencryption.envelope`)
    * Core envelope encryption algorithm and models used
* keymanagement (`com.godaddy.asherah.appencryption.keymanagement`)
    * Interfaces and types related to the external Key Management Service (KMS/HSM)
* persistence (`com.godaddy.asherah.appencryption.persistence`)
    * Interfaces and types related to the Metastore and store/load model's persistence functionality
* crypto (`com.godaddy.asherah.crypto`)
    * Interfaces and types for Crypto Policy and any cryptographic functionality for internal data key generation
* crypto.keys (`com.godaddy.asherah.crypto.keys`)
    * Interfaces and types for internally-generated data key management and caching

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


## Secure Memory

### Current Implementation

Secure Memory is implemented using well known native calls that ensure various protections of a secret value in memory.
Below we describe the pseudocode a Secure Memory implementation needs to perform to properly protect memory. Note the calls
will refer to `libc`-specific implementation. In the future, if we add support for Windows we'll update this page with
corresponding calls appropriately.

#### Create a Secret

```java
ProtectedMemorySecret(byte[] secret) {
    // check rlimit to make sure we won't exceed limit
    get memlock rlimit from system
    if memlock not unlimited and will be exceeded by secret {
        THROW ERROR memlock rlimit will be exceeded by allocation
    }

    // TODO allocate memory with blah blah protections (explain what this all means)
    pointer = mmap(addr = NULL, length = <secret.length>, prot = (PROT_READ | PROT_WRITE),
                   flags = (MAP_PRIVATE | MAP_ANONYMOUS), fd = -1, offset = 0)

    // lock virtual address space into memory, preventing it from being paged to swap/disk
    error = mlock(addr = pointer, len = <secret.length>)
    if error {
        // deallocate memory
        munmap(addr = pointer, len = <secret.length>)
        THROW ERROR mlock failed
    }

    // advise kernel not to include the memory space in core dumps.
    // NOTE: for MacOS, madvise not available, so we disable core dumps globally via
    // "setrlimit(resource = RLIMIT_CORE, rlim = (cur = 0, max = 0))"
    error = madvise(addr = pointer, length = <secret.length>, advice = MADV_DONTDUMP)
    if error {
        // unlock virtual address space from memory, allowing it to be paged to swap/disk
        munlock(addr = pointer, len = <secret.length>)
        // deallocate memory
        munmap(addr = pointer, len = <secret.length>)
        THROW ERROR madvise failed
    }

    // write the secret
    pointer.write(secret)

    // disable all memory access
    mprotect(addr = pointer, len = <secret.length>, prot = PROT_NONE)

    // wipe input bytes
    arrayFillZero(secret)
}
```

#### Use a Secret

```java
withSecretBytes(function<byte[], type> functionWithSecret) {
    bytes = new byte[length]
    try {
        // change memory page access to read-only
        mprotect(addr = pointer, len = <length>, prot = PROT_READ)

        try {
            // read the secret into local variable
            pointer.read(0, bytes, 0, bytes.length)
        }
        finally {
            // always disable all memory access
            mprotect(addr = pointer, len = <length>, prot = PROT_NONE)
        }

        return functionWithSecret(bytes)
    }
    finally {
        // always wipe local variable
        arrayFillZero(bytes)
    }
}
```

#### Delete a Secret

```java
close() {
    // change memory page access to read-write
    mprotect(addr = pointer, len = <length>, prot = (PROT_READ | PROT_WRITE))

    try {
      // use platform specific zero memory function that can't be optimized away.
      // for MacOS, use memset_s(dest = pointer, destSize = <length>, c = 0, count = <length>)
      bzero(addr = pointer, length = <length>)
    }
    finally {
        try {
            // always unlock virtual address space from memory, allowing it to be paged to swap/disk
            munlock(addr = pointer, len = <secret.length>)
        }
        finally {
            // always free memory
            munmap(addr = pointer, len = <secret.length>)
        }
    }
}
```

### Future Work

We plan to investigate the feasibility of replacing the current Secure Memory implementation with calls to a common
library such as OpenSSL, BoringSSL, etc. The intent of this effort would be to see if we can provide even stronger
memory protections, refactor existing crypto calls to use the selected library, and provide more cross-language
implementation consistency.
