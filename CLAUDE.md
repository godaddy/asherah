# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Asherah is an application-layer encryption SDK that provides advanced encryption features and defense in depth against compromise. It supports multiple languages (Java, Go, C#/.NET) and includes a gRPC server layer.

**Important**: The Go implementation is considered the canonical implementation. The C# and Java implementations are somewhat legacy, although they are still used in production.

## Build and Test Commands

### Java
```bash
# Build
cd java/app-encryption
mvn compile

# Run tests
mvn test

# Run integration tests
mvn verify -Dskip.surefire.tests

# Run tests with coverage
mvn test jacoco:report

# Run checkstyle
mvn checkstyle:check

# Run a single test
mvn test -Dtest=TestClassName#testMethodName
```

### Go
```bash
# Build
cd go/appencryption
go build ./...

# Run tests
go test -race ./...

# Run tests with coverage
go test -race -coverprofile coverage.out ./...

# Run integration tests (requires gotestsum)
gotestsum -f testname --junitfile junit_integration_results.xml -- -p 1 -race -coverprofile coverage.out -v `go list ./integrationtest/... | grep -v traces`

# Run linting
golangci-lint run --config .golangci.yml

# Run a single test
go test -race -run TestName ./...
```

### C# / .NET
```bash
# Build
cd csharp/AppEncryption
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Run tests with coverage
dotnet test --configuration Release --test-adapter-path:. --logger:"junit;LogFilePath=test-result.xml" /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover
```

### Cross-Language Scripts
```bash
# Run cross-language encryption tests
./tests/cross-language/scripts/encrypt_all.sh

# Run cross-language decryption tests
./tests/cross-language/scripts/decrypt_all.sh
```

## Code Architecture

### Key Hierarchy
The SDK implements a hierarchical key model with envelope encryption:
- **Master Key (MK)**: Root key managed by HSM/KMS
- **System Key (SK)**: KEK encrypted by MK, scoped to service/subsystem
- **Intermediate Key (IK)**: KEK encrypted by SK, scoped to user/account
- **Data Row Key (DRK)**: DEK encrypted by IK, generated per encryption operation

### Core Components

1. **Session Factory**: Entry point for creating encryption sessions
   - Configured with metastore, KMS, and crypto policy
   - Manages session lifecycle and caching

2. **Metastore**: Pluggable key storage layer
   - In-memory, RDBMS, and DynamoDB implementations
   - Stores encrypted intermediate and system keys

3. **Key Management Service (KMS)**: Pluggable master key provider
   - AWS KMS, static (testing), and custom implementations
   - Manages root key encryption/decryption

4. **Crypto Policy**: Configures key rotation and caching behavior
   - Key expiration intervals
   - Cache TTL settings
   - Rotation strategies

### Language-Specific Patterns

**Java**: Uses builder pattern extensively, heavy use of interfaces
- Main package: `com.godaddy.asherah.appencryption`
- Test naming: `*Test.java`, `*IT.java` for integration tests

**Go**: Idiomatic Go with interfaces and struct embedding
- Main package: `github.com/godaddy/asherah/go/appencryption`
- Uses context.Context for cancellation and deadlines

**C#**: Modern .NET patterns with async/await
- Namespace: `GoDaddy.Asherah.AppEncryption`
- Uses dependency injection patterns

## Important Implementation Notes

1. **Thread Safety**: All session factories and sessions are thread-safe
2. **Memory Protection**: Uses secure memory for key storage (off-heap in Java, locked memory in Go/C#)
3. **Key Caching**: Implements multi-level caching with configurable TTLs
4. **Partition Scoping**: All encryption operations are scoped to a partition ID
5. **Error Handling**: Fail-fast approach - operations fail completely rather than partially

## Testing Approach

- Unit tests for individual components
- Integration tests with real metastores and KMS providers
- Parameterized tests covering all key state combinations
- Cross-language compatibility tests
- Multi-threaded stress tests
- Benchmark tests for performance validation