# Asherah AppEncryption Integration Tests

This directory contains integration tests for the Asherah AppEncryption Rust implementation. The tests are designed to verify that the Rust implementation behaves the same way as the Go implementation across various scenarios and configurations.

## Test Categories

The tests are organized into several categories:

1. **Memory-based tests**: Basic tests using in-memory metastore
2. **DynamoDB tests**: Tests for DynamoDB metastore including global tables
3. **SQL tests**: Tests for MySQL and PostgreSQL metastore implementations
4. **Multi-threaded tests**: Tests to verify concurrent access to sessions
5. **Metastore interaction tests**: Tests to verify correct metastore behavior
6. **Cache behavior tests**: Tests for cache behaviors under different configurations
7. **Parameterized tests**: Tests with different configurations

## Running the Tests

To run all the tests:

```bash
cargo test --test integration_tests
```

To run only the quick tests (skipping slow tests):

```bash
SKIP_SLOW_TESTS=1 cargo test --test integration_tests
```

To run tests for a specific category:

```bash
cargo test --test integration_tests memory
cargo test --test integration_tests dynamodb
cargo test --test integration_tests mysql
cargo test --test integration_tests postgres
cargo test --test integration_tests multithreaded
```

## Test Architecture

The tests use the following components:

- **Test fixtures**: Helper structs to set up test environments
- **Mock implementations**: For testing without external dependencies
- **Docker containers**: For testing with real databases (via testcontainers)
- **Parameterized tests**: To test with different configurations

## Requirements

- Rust toolchain
- Docker (for database tests)
- AWS credentials (for real DynamoDB tests, optional)

## Configuration

The tests can be configured with environment variables:

- `SKIP_SLOW_TESTS`: Set to any value to skip slow tests
- `DISABLE_TESTCONTAINERS`: Set to any value to disable Docker container tests
- `AWS_REGION`: AWS region for DynamoDB tests (if real DynamoDB is used)
- `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`: AWS credentials (if real DynamoDB is used)

## Note on Mock Implementations

Some tests use mock implementations (e.g., `MockDynamoDb`) to avoid external dependencies. In a real production environment, you would use the actual implementations with real AWS services.