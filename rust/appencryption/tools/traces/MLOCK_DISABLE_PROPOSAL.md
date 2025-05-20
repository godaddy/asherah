# Proposal: Add Flag to Disable Memory Locking for Benchmarking

## Problem Statement

Currently, benchmarks for the securememory library fail on systems with memory lock limits, making it difficult to run performance comparisons. This is particularly problematic on macOS/Darwin where memory lock limits are restrictive.

## Proposed Solution

Add conditional compilation flags to disable memory locking specifically for benchmarking purposes.

### Rust Implementation

Add a `no-mlock` feature flag in the `securememory` crate:

1. Update `Cargo.toml`:
```toml
[features]
default = []
no-mlock = []  # Disable memory locking for benchmarking
```

2. Update `src/protected_memory/secret.rs`:
```rust
// Line 138-140
#[cfg(not(feature = "no-mlock"))]
memcall::lock(&mut memory)
    .map_err(|e| SecureMemoryError::MemoryLockFailed(e.to_string()))?;
```

3. Run benchmarks with:
```bash
cargo bench --features no-mlock
```

### Go Implementation

Add a build tag to disable memory locking:

1. Create `secret_nobenchlock.go`:
```go
//go:build nobenchlock
// +build nobenchlock

package protectedmemory

// Override the lock function when nobenchlock is specified
func (mc *memcall) Lock(b []byte) error {
    // No-op for benchmarking
    return nil
}
```

2. Run benchmarks with:
```bash
go test -bench=. -tags=nobenchlock
```

## Security Considerations

⚠️ **WARNING**: This flag must ONLY be used for benchmarking and testing. Never use in production as it defeats the security purpose of the library.

The flag names are intentionally verbose (`no-mlock`, `nobenchlock`) to make it clear they're special-purpose and not for general use.

## Benefits

1. Enables performance benchmarking on systems with memory lock limits
2. Allows CI/CD systems to run benchmarks without special privileges
3. Facilitates performance comparison between Go and Rust implementations
4. Makes development and testing easier on macOS

## Implementation Priority

Priority should be given to the Rust implementation as it's currently blocking benchmark execution.