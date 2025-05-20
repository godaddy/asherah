# Platform Support in SecureMemory

This document describes the platform support provided by the SecureMemory crate.

## Supported Platforms

SecureMemory provides platform-specific implementations for the following operating systems:

- Linux
- macOS
- Windows
- FreeBSD
- NetBSD
- OpenBSD
- Solaris

## Platform-Specific Features

### Linux

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `madvise` with `MADV_DONTDUMP` to prevent memory from being included in core dumps
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS` for memory allocation
- Uses `mprotect` to control memory access permissions

### macOS

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS` for memory allocation
- Uses `mprotect` to control memory access permissions

### Windows

- Uses `VirtualLock` to prevent memory from being swapped to disk
- Uses `VirtualAlloc` for memory allocation
- Uses `VirtualProtect` to control memory access permissions
- Uses `CryptProtectMemory` for additional memory protection on Windows

### FreeBSD

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `madvise` with `MADV_NOCORE` to prevent memory from being included in core dumps
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS | MAP_NOCORE` for memory allocation
- Uses `mprotect` to control memory access permissions

### NetBSD

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS` for memory allocation
- Uses `mprotect` to control memory access permissions

### OpenBSD

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS | MAP_CONCEAL` for memory allocation
- Uses `mprotect` to control memory access permissions

### Solaris

- Uses `mlock` to prevent memory from being swapped to disk
- Uses `mmap` with `MAP_PRIVATE | MAP_ANONYMOUS` for memory allocation
- Uses `mprotect` to control memory access permissions

## Core Dump Prevention

On all Unix-like systems (Linux, macOS, FreeBSD, NetBSD, OpenBSD, Solaris), SecureMemory provides a `disable_core_dumps()` function that sets the core file size limit to zero using `setrlimit(RLIMIT_CORE, ...)`.

## Memory Protection States

SecureMemory provides three memory protection states:

1. `NoAccess`: Memory is not accessible (can't read or write)
2. `ReadOnly`: Memory is read-only (can read but can't write)
3. `ReadWrite`: Memory is readable and writable

## Usage Recommendations

It's recommended to:

1. Lock sensitive memory to prevent it from being swapped to disk
2. Use the NoAccess protection when the memory is not actively being used
3. Use ReadOnly protection when the data should not be modified
4. Only use ReadWrite protection when actively modifying the data
5. Disable core dumps to prevent sensitive data from being included in crash dumps