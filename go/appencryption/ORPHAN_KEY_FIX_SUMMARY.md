# Cache Eviction Orphan Key Fix Summary

## Problem
The cache eviction mechanism had a critical flaw where keys with active references would become "orphaned" - removed from the cache but not properly closed, leading to memory leaks.

## Root Cause
In `pkg/cache/cache.go`, the cache removes entries from its map BEFORE calling the eviction callback. If a key still has active references (ref count > 0), the `Close()` method returns early without actually closing the key. This creates orphaned keys that are:
- No longer in the cache (cannot be retrieved)
- Still consuming memory (not closed)
- Lost forever (no way to track or clean them up)

## Solution: Minimal Change Approach
Added orphaned key tracking to `key_cache.go`:

1. **Modified `Close()` to return bool** - indicates whether the key was actually closed
2. **Track orphaned keys** - maintain a separate list of keys that failed to close during eviction
3. **Periodic cleanup** - attempt to close orphaned keys every 100 cache accesses
4. **Cleanup on cache close** - ensure orphaned keys are cleaned up when cache is closed

## Implementation Details

### Changes to `key_cache.go`:

1. Added orphan tracking fields:
```go
orphaned []*cachedCryptoKey
orphanedMu sync.Mutex
```

2. Modified eviction callback to track orphans:
```go
if !value.key.Close() {
    c.orphanedMu.Lock()
    c.orphaned = append(c.orphaned, value.key)
    c.orphanedMu.Unlock()
}
```

3. Added cleanup function:
```go
func (c *keyCache) cleanOrphaned() {
    // Attempts to close orphaned keys
    // Keeps only those still referenced
}
```

4. Integrated cleanup into cache lifecycle:
- Called periodically during `GetOrLoad`
- Called during `Close()`

## Benefits
- **Prevents memory leaks** - orphaned keys are eventually cleaned up
- **Minimal change** - doesn't require modifying third-party cache library
- **Thread-safe** - uses mutex to protect orphaned list
- **No performance impact** - cleanup is infrequent and synchronous

## Testing
- Verified orphaned keys are tracked when eviction fails
- Confirmed cleanup removes keys once references are released
- Ensured thread safety with concurrent access
- All existing tests continue to pass