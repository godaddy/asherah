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


## Algorithm Internals

### Write 
Depending on policy, we will either continue to write if a key in the tree has expired or rotate/generate keys inline.

    Data is ready to write to data persistence
    If latest IK is not cached or latest IK in cache is expired
        Load latest IK EKR from metadata persistence
        If IK is found
            If IK is not expired or (IK is expired and policy allows queued rotation)
                If SK is not cached
                    Load specific SK EKR from metadata persistence
                    If SK EKR DOES NOT exist in metadata persistence
                        Fall through to new IK creation
                    If allowed by policy, add SK to protected memory cache
                If SK is expired
                    # NOTE: Possible inconsistency: when policy doesn't use inline rotation, consider proceeding without forced creation (same as IK handling)
                    Fall through to new IK creation
                Else
                    Use SK to decrypt IK
            Else
                Fall through to new IK creation
        Else (new IK being created)
            If latest SK is not cached or latest SK in cache is expired
                Load latest SK EKR from metadata persistence
                If SK is found
                    If SK is not expired or (SK is expired and policy allows queued rotation)
                        Use MK in HSM to decrypt SK
                    Else
                        Fall through to new SK creation
                Else (new SK being created)
                    Create new SK with crypto library (e.g. openssl)
                    Use MK in HSM to encrypt SK
                    Attempt to write SK EKR in metadata persistence
                    If SK EKR write failed due to duplicate (race condition with other thread)
                        Load latest SK EKR from metadata persistence
                        Use MK in HSM to decrypt SK
                If allowed by policy, add SK to protected memory cache
            Create new IK with crypto library (e.g. openssl)
            Use SK to encrypt IK
            Attempt to write IK EKR in metadata persistence
                If IK EKR write failed due to duplicate (race condition with other thread)
                    Load latest IK EKR from metadata persistence
                    If SK is not cached
                        Load specific SK EKR from metadata persistence
                        If SK EKR DOES NOT exist in metadata persistence
                            THROW ERROR: Unable to decrypt IK, missing SK from metadata (shouldn't happen)
                        Use MK in HSM to decrypt SK
                        If allowed by policy, add SK to protected memory cache
                    If SK is expired
                        THROW ERROR: system key expired (shouldn't happen, other thread would've created one)
                    Use SK to decrypt IK
            If allowed by policy, add IK to protected memory cache
    Create new DRK with crypto library (e.g. openssl)
    Use DRK to encrypt Data
    Use IK to encrypt DRK
    Create and write DRR to data persistence

### Read

    Load DRR from data persistence
    Extract IK meta from DRR
    If IK is not cached
        Load specific IK EKR from metadata persistence    
        If IK EKR DOES NOT exist in metadata persistence
            THROW ERROR: Unable to decrypt DRK, missing IK from metadata
        Extract SK meta from IK EKR
        If SK is not cached
            Load specific SK EKR from metadata persistence
            If SK EKR DOES NOT exist in metadata persistence
                THROW ERROR: Unable to decrypt IK, missing SK from metadata
            Use MK in HSM to decrypt SK
            If allowed by policy, add SK to protected memory cache
        If SK is expired
            # NOTE: None of these currently implemented
            Send notification SK is expired
            Queue SK for rotation
            Queue IK for rotation
            Queue DRK for rotation
        Use SK to decrypt IK
        If allowed by policy, add IK to protected memory cache
    If IK is expired
        # NOTE: None of these currently implemented
        Send notification IK is expired
        Queue IK for rotation
        Queue DRK for rotation #We'll continue to wind up here until we write with valid key
    Use IK to decrypt DRK
    Use DRK to decrypt Data
    If DRK is expired
        # NOTE: Not currently implemented
        Queue DRK for rotation


### Future Consideration: Queued Rotation
There has been a shift from the original queue-based key rotation towards generating new keys inline by default (for the write path).

Below are the original proposed queue rotation flows.

#### MK Rotation

    This happens annually
    Update the policy to expire all the keys
    Once it does:
        Queue All SKs for rotation
        Queue All IKs for rotation
        Queue All DRKs for rotation #Specific for each user - this is stored in the application


#### SK Rotation

    Read message from FIFO SK_IK key rotation queue
    If SK message meta = current SK meta in metadata persistence 
        Load SK EKR from metadata persistence 
        Use MK in HSM to create and encrypt a new SK
        Create and write new SK EKR in metadata persistence 
    Delete message

#### IK Rotation

    Read message from FIFO SK_IK key rotation queue
    If IK meta in message = current IK in metadata persistence
        If SK EKR DOES NOT exist in metadata persistence 
            THROW ERROR: no SK exists
        Load current SK EKR from metadata persistence
        Use MK in HSM to decrypt SK
        If SK is expired
            Queue SK for rotation
            Queue IK for rotation
        Else 
            Create new IK from crypto library (i.e. openssl)
            Use SK to encrypt IK
            Create and write new IK EKR in metadata persistence 
    Delete message

#### DRK Rotation - POTENTIAL RACE CONDITION

    Read message from standard DRK key rotation queue
    Load DRK EKR from message
    If IK is not cached 
        Load current IK from metadata persistence 
        If SK in IK EKR is not cached
            Load current SK from metadata persistence 
            Use MK in HSM to decrypt SK
        If SK is expired
            Queue SK for rotation
            Queue IK for rotation
            Exit #We'll be back once SK has rotated
        Use SK to decrypt IK
    If IK is expired
        Queue IK for rotation
        Exit  #We'll be back once IK has rotated
    Create new DRK from crypto library (i.e. openssl)
    Load DRR from data persistence
    Use DRK to encrypt data
    Use IK to encrypt DRK
    Load DRR from data persistence AGAIN
    If DRK EKR matches DRR EKR
        #Warning potential race condition starts here
        Update existing DRR in data persistence 
        #We could have just overwritten a user's write
    Delete Message

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
