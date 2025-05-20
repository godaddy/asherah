# Implementation Notes

## Platform-Specific Implementations

The following files provide platform-specific implementations for memory management:

- `src/mem_sys/unix.rs`: For Linux and macOS
- `src/mem_sys/freebsd.rs`: For FreeBSD
- `src/mem_sys/netbsd.rs`: For NetBSD
- `src/mem_sys/openbsd.rs`: For OpenBSD
- `src/mem_sys/solaris.rs`: For Solaris
- `src/mem_sys/windows.rs`: For Windows

### Key Differences Between Platforms

1. **FreeBSD**
   - Uses `MAP_NOCORE` flag during memory allocation to prevent memory from being included in core dumps
   - Uses `madvise` with `MADV_NOCORE` to further prevent memory from being included in core dumps

2. **NetBSD**
   - Uses standard Unix memory protection without additional flags
   - No platform-specific memory protection flags

3. **OpenBSD**
   - Uses `MAP_CONCEAL` flag during memory allocation, which is an OpenBSD-specific flag to help hide memory mappings from other processes
   - Provides enhanced security for sensitive memory regions

4. **Solaris**
   - Uses standard Unix memory protection without additional flags
   - No platform-specific memory protection flags

## Implementation Details

### Memory Allocation

All platforms use `mmap` for memory allocation, but with different flags depending on the platform:

- FreeBSD: `MAP_PRIVATE | MAP_ANON | MAP_NOCORE`
- OpenBSD: `MAP_PRIVATE | MAP_ANON | MAP_CONCEAL`
- NetBSD/Solaris: `MAP_PRIVATE | MAP_ANON`

### Memory Protection

All platforms use `mprotect` to control memory access permissions with standard protection flags:

- `PROT_NONE`: No access (can't read or write)
- `PROT_READ`: Read-only access
- `PROT_READ | PROT_WRITE`: Read-write access

### Memory Locking

All platforms use `mlock` to prevent memory from being swapped to disk and `munlock` to unlock it.

### Core Dump Prevention

All Unix-like platforms disable core dumps using `setrlimit(RLIMIT_CORE, ...)` to set the core file size limit to zero.

## Testing

Platform-specific implementations are tested in `tests/platform_specific_test.rs`. This test verifies:

1. Memory allocation and freeing
2. Memory protection (ReadWrite, ReadOnly, NoAccess)
3. Memory locking and unlocking
4. Core dump disabling

## Cross-Compilation and Testing

To test the implementations on different platforms, you can use cross-compilation with appropriate target triples:

- FreeBSD: `--target x86_64-unknown-freebsd`
- NetBSD: `--target x86_64-unknown-netbsd`
- OpenBSD: `--target x86_64-unknown-openbsd`
- Solaris: `--target x86_64-sun-solaris`

For example:
```
cargo build --target x86_64-unknown-freebsd
```

Note that you'll need the appropriate target installed via rustup:
```
rustup target add x86_64-unknown-freebsd
```