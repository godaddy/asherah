# Asherah Go Implementation - Remediation Guide

This document outlines critical issues found in the Asherah Go implementation that require remediation, organized by severity and impact on high-traffic production systems.

## 🔴 Critical Security Issues

### 1. Panic on Random Number Generation Failure
**Location**: `internal/bytes.go:26-28`
```go
if _, err := r(buf); err != nil {
    panic(err)
}
```

**Why Fix**:
- Entropy exhaustion is a real scenario in containerized environments or VMs
- Panicking prevents graceful degradation or retry logic
- In production, this causes service crashes instead of temporary failures
- Cannot implement circuit breakers or fallback strategies

**Remediation**:
- Change `FillRandom` to return an error instead of panicking
- Propagate errors up to callers who can implement retry logic
- Add monitoring/alerting for entropy failures

## 🟢 Other Notable Issues

### 1. Silent Error Swallowing
**Location**: `envelope.go:221`
```go
_ = err // err is intentionally ignored
```

**Why Fix**:
- Masks critical infrastructure failures (network, permissions, etc.)
- Makes debugging production issues nearly impossible
- Treats all errors as "duplicate key" when they could be systemic
- No observability into metastore health

**Remediation**:
- Log errors with appropriate severity
- Add metrics/monitoring for metastore failures
- Implement error classification (retriable vs permanent)

### 2. Resource Leak on Close Error
**Location**: `session.go:99-100`
```go
if f.Config.Policy.SharedIntermediateKeyCache {
    f.intermediateKeys.Close()
}
return f.systemKeys.Close()
```

**Why Fix**:
- First Close() error is lost if second fails
- Leaves resources (memory, file handles) leaked
- In long-running services, accumulates resource leaks
- Makes it hard to diagnose which component failed

**Remediation**:
- Collect all errors using `multierr` or similar
- Ensure all resources are attempted to be closed
- Return combined error with full context

## Priority Order for Remediation

1. **Immediate (Security Critical)**:
   - Panic on RNG failure (#1)

2. **Lower Priority (Observability)**:
   - Silent error swallowing (Other #1)
   - Resource leak on close error (Other #2)

## Testing Recommendations

- Add benchmarks for all hot paths with allocation tracking
- Implement stress tests with high concurrency
- Add fuzzing for error path handling
- Use race detector in all tests (`go test -race`)
- Add memory leak detection tests