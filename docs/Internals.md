# Common APIs and Algorithm Internals



## Secure Memory

### Current Implementation

Secure Memory is implemented using well known native calls that ensure various protections of a secret value in memory.
Below we describe the calls a Secure Memory implementation needs to perform to properly protect memory. Note the calls
will refer to `libc`-specific implementation. In the future, if we add support for Windows we'll update this page with
corresponding calls appropriately.

#### Create a Secret

```
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

    // TODO Need to finalize error handling in remaining calls. Inconsistency in java vs C# currently

    // write the secret
    pointer.write(secret)

    // disable all memory access
    mprotect(addr = pointer, len = <secret.length>, prot = PROT_NONE)

    // wipe input bytes
    arrayFillZero(secret)
}
```

#### Use a Secret

```
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

```
close() {
    // change memory page access to read-write
    mprotect(addr = pointer, len = <length>, prot = (PROT_READ | PROT_WRITE))

    try {
      # use platform specific zero memory function that can't be optimized away.
      # for MacOS, use memset_s(dest = pointer, destSize = <length>, c = 0, count = <length>)
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
