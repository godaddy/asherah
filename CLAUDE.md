# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with the Asherah encryption SDK.

## Project Overview
Asherah is an application-layer encryption SDK providing envelope encryption with hierarchical key management, secure memory handling, and defense in depth against compromise. It supports multiple languages with pluggable KMS and metastore backends.

## Build Commands
- C#: `cd csharp/<Project> && ./scripts/build.sh` or `dotnet build`
- Go: `cd go/<module> && ./scripts/build.sh` or `go build ./...`
- Java: `cd java/<module> && ./scripts/build.sh` or `mvn compile`
- Server: `cd server/<language> && ./scripts/build.sh`

## Test Commands
- Full test suites: `./scripts/test.sh` (in respective language directories)
- Integration tests: `./scripts/integration_test.sh` (where available)
- C# single test: `dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName"`
- Go single test: `go test -run TestName ./path/to/package`
- Java single test: `mvn -Dtest=TestClassName#methodName test`
- Cross-language tests: `cd tests/cross-language && ./scripts/encrypt_all.sh && ./scripts/decrypt_all.sh`

## Lint Commands
- C#: `dotnet format` for formatting, built-in analyzers
- Go: Binary installation via `curl -sSfL https://raw.githubusercontent.com/golangci/golangci-lint/master/install.sh | sh -s -- -b $(go env GOPATH)/bin v2.9.0`
  - Alternatively: `brew install golangci-lint` (installs latest v2)
  - Version should match CI configuration (currently v2.9.0)
  - Run with: `golangci-lint run` in module directory
- Java: Checkstyle via Maven (configured in pom.xml)

## Environment Setup
Common test environment variables:
```bash
export AWS_ACCESS_KEY_ID=dummykey
export AWS_SECRET_ACCESS_KEY=dummysecret
export AWS_DEFAULT_REGION=us-west-2
export MYSQL_HOSTNAME=localhost
export DYNAMODB_HOSTNAME=localhost
```

## Key Components
- **AppEncryption**: Core encryption libraries (C#, Go, Java)
- **SecureMemory**: Protected memory allocation (C#, Go, Java)
- **Server**: gRPC service layer (Go, Java)
- **Samples**: Reference implementations and AWS deployment examples

## Testing Infrastructure
- Uses TestContainers for integration testing (DynamoDB, MySQL)
- Docker Compose files for local development (`samples/` directory)
- Cross-language compatibility tests verify encrypt/decrypt operations work across all SDKs

## Style Guidelines
- C#: Microsoft guidelines, 120 char line limit, prefer method overloading over optional args
- Go: Standard idioms, proper error handling with error propagation
- Java: Google's style guide with Checkstyle enforcement

## Security Patterns
- Envelope encryption with hierarchical key model (System Key → Intermediate Key → Data Encryption Key)
- Protected memory handling for sensitive data (off-heap, secure wiping)
- Session-based encryption with partition isolation
- KMS integration for root key management (AWS KMS, Static keys for testing)
- Pluggable metastore backends (DynamoDB, RDBMS, In-Memory)
