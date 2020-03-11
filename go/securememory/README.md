# Secure Memory Go Edition

This package provides a way for applications to keep secret information (like cryptographic keys) in an area of memory
that is secure in the described ways.

## Currently supported / tested platforms
* MacOS x86-64
* Linux x86-64

## Guarantees
Any implementation must have the following guarantees in so far as secret information stored in secure memory

* Values stored will not show up in core dumps
* Values stored will not be swapped
* Values stored will be securely / explicitly zeroed out when no longer in use

## Protected Memory Implementation
The protected memory implementation of secure memory:

* Uses mlock to lock the pages to prevent swapping
* Uses mprotect to mark the pages no-access until needed
* Uses setrlimit to disable core dumps entirely

### Usage

```go
package main

import (
    "fmt"
    
    "github.com/godaddy/asherah/go/securememory"
    "github.com/godaddy/asherah/go/securememory/protectedmemory"
)

func main() {
    factory := new(protectedmemory.SecretFactory)

    secret, err := factory.New(getSecretFromStore())
    if err != nil {
        panic("unexpected error!")
    }
    defer secret.Close()

    err = secret.WithBytes(func(b []byte) error {
        doSomethingWithSecretBytes(b)
        return nil
    })
    if err != nil {
        panic("unexpected error!")
    }
}
```

## Memguard Implementation
The [memguard](https://github.com/awnumar/memguard/)-based implementation has identical guarantees as the protected
memory implementation. It also makes use of guard pages and canary data to add further protections. Note the user of
the guard pages will add an additional 2 pages of unmanaged memory being used per Secret (as of this writing).

In addition, we have included the no-access support that we have provided in our other language implementations.

The memguard based implementation:

* Uses mlock to lock the pages to prevent swapping
* Uses mprotect to mark the pages no-access until needed
* Uses setrlimit to disable core dumps entirely

### Usage

```go
package main

import (
    "fmt"
    
    "github.com/godaddy/asherah/go/securememory"
    "github.com/godaddy/asherah/go/securememory/memguard"
)

func main() {
    factory := memguard.SecretFactory{}

    secret, err := factory.New(getSecretFromStore())
    if err != nil {
        panic("unexpected error!")
    }
    defer secret.Close()

    err = secret.WithBytes(func(b []byte) error {
        doSomethingWithSecretBytes(b)
        return nil
    })
    if err != nil {
        panic("unexpected error!")
    }
}
```

## TODO
* Add unit tests that generate core dumps and scan them for secrets (need to extract gcore source)
* If the operating system supports it, uses madvise to request that mapped pages be zeroed on exit
* If the operating system supports it, uses madvise to disable core dumps for the data region instead of globally
