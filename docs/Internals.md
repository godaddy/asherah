# SDK Internals

## Envelope Encryption Algorithm

### Encrypt

Depending on policy, we will either continue to encrypt if a key in the tree has expired or rotate/generate keys inline.

```
Data is ready to write to data persistence
If latest IK is not cached or latest IK in cache is expired
    Load latest IK EKR from metastore
    If IK is found
        If IK is not expired or (IK is expired and policy allows queued rotation)
            If SK is not cached
                Load specific SK EKR from metastore
                If SK EKR DOES NOT exist in metastore
                    Fall through to new IK creation
                If allowed by policy, add SK to protected memory cache
            If SK is expired
                # NOTE: Possible inconsistency: when policy doesn't use inline rotation, consider proceeding without
                #       forced creation (same as IK handling)
                Fall through to new IK creation
            Else
                Use SK to decrypt IK
        Else
            Fall through to new IK creation
    Else (new IK being created)
        If latest SK is not cached or latest SK in cache is expired
            Load latest SK EKR from metastore
            If SK is found
                If SK is not expired or (SK is expired and policy allows queued rotation)
                    Use MK in HSM to decrypt SK
                Else
                    Fall through to new SK creation
            Else (new SK being created)
                Create new SK with crypto library (e.g. openssl)
                Use MK in HSM to encrypt SK
                Attempt to write SK EKR in metastore
                If SK EKR write failed due to duplicate (race condition with other thread)
                    Load latest SK EKR from metastore
                    Use MK in HSM to decrypt SK
            If allowed by policy, add SK to protected memory cache
        Create new IK with crypto library (e.g. openssl)
        Use SK to encrypt IK
        Attempt to write IK EKR in metastore
            If IK EKR write failed due to duplicate (race condition with other thread)
                Load latest IK EKR from metastore
                If SK is not cached
                    Load specific SK EKR from metastore
                    If SK EKR DOES NOT exist in metastore
                        THROW ERROR: Unable to decrypt IK, missing SK from metastore (shouldn't happen)
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
```

The following diagram summarizes the entire encrypt path.

![Encrypt Flow](https://raw.githubusercontent.com/godaddy/asherah/master/docs/images/encrypt.svg?sanitize=true)

### Decrypt

```
Load DRR from data persistence
Extract IK meta from DRR
If IK is not cached
    Load specific IK EKR from metastore
    If IK EKR DOES NOT exist in metastore
        THROW ERROR: Unable to decrypt DRK, missing IK from metastore
    Extract SK meta from IK EKR
    If SK is not cached
        Load specific SK EKR from metastore
        If SK EKR DOES NOT exist in metastore
            THROW ERROR: Unable to decrypt IK, missing SK from metastore
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
Return decrypted data
```

The following diagram summarizes the entire decrypt path.

![Decrypt Flow](https://raw.githubusercontent.com/godaddy/asherah/master/docs/images/decrypt.svg?sanitize=true)

### Future Consideration: Queued Rotation

Below are the proposed queue rotation flows.

#### MK Rotation

```
This happens annually
Update the policy to expire all the keys
Once it does:
    Queue All SKs for rotation
    Queue All IKs for rotation
    Queue All DRKs for rotation #Specific for each user - this is stored in the application
```

#### SK Rotation

```
Read message from FIFO SK_IK key rotation queue
If SK message meta = current SK meta in metastore
    Load SK EKR from metastore
    Use MK in HSM to create and encrypt a new SK
    Create and write new SK EKR in metastore
Delete message
```

#### IK Rotation

```
Read message from FIFO SK_IK key rotation queue
If IK meta in message = current IK in metastore
    If SK EKR DOES NOT exist in metastore
        THROW ERROR: no SK exists
    Load current SK EKR from metastore
    Use MK in HSM to decrypt SK
    If SK is expired
        Queue SK for rotation
        Queue IK for rotation
    Else
        Create new IK from crypto library (e.g. openssl)
        Use SK to encrypt IK
        Create and write new IK EKR in metastore
Delete message
```

#### DRK Rotation - POTENTIAL RACE CONDITION

```
Read message from standard DRK key rotation queue
Load DRK EKR from message
If IK is not cached
    Load current IK from metastore
    If SK in IK EKR is not cached
        Load current SK from metastore
        Use MK in HSM to decrypt SK
    If SK is expired
        Queue SK for rotation
        Queue IK for rotation
        Exit #We'll be back once SK has rotated
    Use SK to decrypt IK
If IK is expired
    Queue IK for rotation
    Exit  #We'll be back once IK has rotated
Create new DRK from crypto library (e.g. openssl)
Load DRR from data persistence
Use DRK to encrypt data
Use IK to encrypt DRK
Load DRR from data persistence AGAIN
If DRK EKR matches DRR EKR
    #Warning potential race condition starts here
    Update existing DRR in data persistence
    #We could have just overwritten a user's write
Delete Message
```

## Secure Memory

### Current Implementation

Secure Memory is implemented using well known native calls that ensure various protections of a secret value in memory.
Below we describe the pseudocode a Secure Memory implementation needs to perform to properly protect memory. Note the
calls will refer to `libc`-specific implementation. In the future, if we add support for Windows we'll update this
page with corresponding calls appropriately.

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

##### Concurrent Access

The `withSecretBytes` pseudocode above is not thread-safe code as written. A thread could disable the memory access as
another thread attempts to read the secret, which would result in a SIGSEGV signal to the process. Some form of thread
safety is needed to guard against this. For example, this could be implemented using a lock and access counter to
determine when we need to make the memory readable (first thread accessing) or unreadable (last thread accessing).

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

## Key Cache

### TTL and Expired/Revoked Keys

The [Crypto Policy](CryptoPolicy.md)'s `revokeCheckPeriodMillis` drives the key cache implementation's TTL behavior.
The TTL is primarily intended to signal refreshing the cache so the SDK can check if keys have been flagged out-of-band
as revoked (e.g. due to a suspected compromise). Note that in the Java reference implementation, we are not currently
removing expired or revoked keys from the cache. This approach was chosen to minimize the added latency and cost
associated with interacting with a KMS/HSM provider (recall TTL is more likely to come into play with System Keys due
to their [intended lifecycle](KeyCaching.md#cache-lifecycles)).

### Duplicate Key Handling

Since the objects being cached are resources which need to be closed, there is additional complexity when dealing with
duplicates in the cache. The approach taken in the Java reference implementation is to always return the key intended
to be closed to the caller:
* For the case of a new key being added to the cache, we return a new "shared key" representation of the key whose
close operation is a no-op (since the key passed in to the cache put/store call will now be used in the cache by other
threads).
* For the case of a duplicate key being added to the cache (e.g. a race condition's second thread), we return the key
passed in to the cache put/store call so it can be safely closed without affecting the existing underlying key in the
cache and ensuring we don't leak the memory space of the key.
