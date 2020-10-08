# SecureMemory C# Edition
This class provides a way for applications to keep secret information (like cryptographic keys) in an area of memory that is secure in the described ways.

[![Version](https://img.shields.io/nuget/v/Godaddy.Asherah.SecureMemory)](https://www.nuget.org/packages/GoDaddy.Asherah.AppEncryption)

  * [Installation](#installation)
       * [Currently supported / tested platforms](#currently-supported--tested-platforms)
  * [Configuration](#configuration)
  * [Quick Start](#quick-start)
  * [Goals](#goals)
  * [Guarantees](#guarantees)
  * [Protected Memory Implementation](#protected-memory-implementation)

## Installation
You can get the latest release from [Nuget](https://www.nuget.org/packages/GoDaddy.Asherah.SecureMemory/):
```xml
<ItemGroup>
    <PackageReference Include="GoDaddy.Asherah.SecureMemory" Version="0.1.1" />
</ItemGroup>
```

### Currently supported / tested platforms
* MacOS x86-64
* Linux x86-64
* Windows x86-64
  > Initial Windows support is provided primarily for local development


## Configuration

<table>
    <tr>
        <th>Configuration Name</th>
        <th>Data type</th>
        <th>Values</th>
        <th>Default / Required</th>
        <th>Description</th>
    </tr>
    <tr>
        <td>secureHeapEngine</td>
        <td>string</td>
        <td>openssl11, mmap</td>
        <td>Platform default (Usually mmap)</td>
        <td>Controls which secure heap implementation is used</td>
    </tr>
    <tr>
        <td>heapSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>32767</td>
        <td>Size of the secure heap in bytes</td>
    </tr>
    <tr>
        <td>minimumAllocationSize</td>
        <td>int</td>
        <td>Size in bytes</td>
        <td>32</td>
        <td>Minimum size of secure heap allocations</td>
    </tr>
    <tr>
        <td>minimumWorkingSetSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>33554430</td>
        <td>Windows only: Configure the minimum working set size which influences how much memory can be VirtualLocked</td>
    </tr>
    <tr>
        <td>maximumWorkingSetSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>67108860</td>
        <td>Windows only: Configure the maximum working set size which influences how much memory can be VirtualLocked</td>
    </tr>
</table>
<br>

## Quick Start
```c#
ISecretFactory secretFactory = new ProtectedMemorySecretFactory();

using (Secret secretKey = secretFactory.CreateSecret(secretBytes))
{
    secretKey.WithSecretBytes(bytes =>
    {
        DoSomethingWithSecretBytes(bytes);
    });
}
```

## Goals
* Provide an interface which allows safe patterns of secrets usage
* Provide a handle for passing secrets around that doesn't expose the secret until it's actually used
* Minimize the lifetime of secrets in managed memory

## Guarantees
Any implementation must have the following guarantees in so far as secret information stored in secure memory

Linux/Mac:
* Values stored will not show up in core dumps
* Values stored will not be swapped
* Values stored will be securely / explicitly zeroed out when no longer in use

Windows:
* Values are encrypted in memory
* Values stored will not be swapped
* Values stored will be explicitly zeroed out when no longer in use

## Protected Memory Implementation
The protected memory implementation of secure memory

* Uses mlock to lock the pages to prevent swapping
* Uses mprotect to mark the pages no-access until needed
* If the operating system supports it, uses madvise to disallow core dumps of protected regions
* If the operating system does not support madvise, uses setrlimit to disable core dumps entirely

## TODO
* Add unit tests that generate core dumps and scan them for secrets (need to extract gcore source)
* If the operating system supports it, uses madvise to request that mapped pages be zeroed on exit
