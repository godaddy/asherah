# Asherah Rust Examples

This directory contains comprehensive examples demonstrating how to use the Rust implementation of Asherah. These examples showcase various features and integration patterns to help you get started with Asherah in your Rust applications.

## Table of Contents

- [Advanced Stream API](#advanced-stream-api)
- [Custom Metastore Implementation](#custom-metastore-implementation)
- [AWS KMS with Region Failover](#aws-kms-with-region-failover)
- [SQL Metastore Integration](#sql-metastore-integration)
- [Advanced Caching Strategies](#advanced-caching-strategies)
- [Metrics Integration](#metrics-integration)
- [Performance Tuning](#performance-tuning)
- [Web Framework Integration](#web-framework-integration)

## Running the Examples

For most examples, simply use:

```bash
# Navigate to the appencryption directory
cd asherah/rust/appencryption

# Run a specific example
cargo run --example advanced_caching
```

Some examples require additional dependencies or configuration. Check the specific example sections below for details.

## Advanced Stream API

**File:** `/rust/securememory/examples/advanced_stream.rs`

Demonstrates advanced usage of SecureMemory's Stream API for processing sensitive data in chunks:

- Shared stream across threads for parallel processing
- Producer-consumer pattern for secure data handling
- Stream backpressure and timeout handling
- Proper resource cleanup

```bash
cd asherah/rust/securememory
cargo run --example advanced_stream
```

## Custom Metastore Implementation

**File:** `/rust/appencryption/examples/custom_metastore.rs`

Shows how to implement a custom metastore for Asherah:

- Redis-like metastore implementation
- Custom TTL and expiration functionality
- Integration with Asherah's session management
- Proper error handling

## AWS KMS with Region Failover

**File:** `/rust/appencryption/examples/aws_kms_with_failover.rs`

Demonstrates AWS KMS integration with multi-region failover:

- Configuration with multiple AWS regions
- Regional failover for KMS operations
- Setting timeout and retry policies
- Metrics for KMS operations

Requirements:
- AWS credentials configured in your environment
- AWS KMS keys in different regions (for a real implementation)

## SQL Metastore Integration

**File:** `/rust/appencryption/examples/sql_metastore_integration.rs`

Shows how to use SQL databases for key storage:

- MySQL integration using SQLx
- Implementing the SqlClient trait
- Database connection pooling
- Error handling

Requirements:
- MySQL server running locally (or update connection string)
- SQLx library dependencies

## Advanced Caching Strategies

**File:** `/rust/appencryption/examples/advanced_caching.rs`

Explores different caching strategies for optimal performance:

- LRU, LFU, and TLFU cache implementations
- Cache eviction policies
- Time-based expiration
- Performance comparison between strategies

## Metrics Integration

**File:** `/rust/appencryption/examples/metrics_integration.rs`

Demonstrates metrics collection and monitoring:

- Custom metrics provider implementation
- Prometheus integration
- Tracking encryption/decryption latency
- Monitoring cache hit/miss rates

Requirements:
- Prometheus server (optional, for real monitoring)

## Performance Tuning

**File:** `/rust/appencryption/examples/performance_tuning.rs`

Comprehensive guide to performance optimization:

- Comparing different cache configurations
- Parallel vs. sequential operations
- Impact of key rotation policies
- Optimizing for high throughput

## Web Framework Integration

**File:** `/rust/appencryption/examples/actix_web_integration.rs`

Demonstrates integration with the Actix web framework:

- SessionCache for efficient per-user encryption
- REST API for encryption/decryption
- Asynchronous handling of encryption operations
- Error handling in web context

Requirements:
- Actix Web dependencies (specified in the example)

```bash
# Install required dependencies
cargo add actix-web serde base64

# Run the example
cargo run --example actix_web_integration
```

## Additional Resources

- [Asherah Documentation](https://github.com/godaddy/asherah/tree/main/docs)
- [Rust Implementation API Documentation](https://docs.rs/appencryption)
- [SecureMemory Documentation](https://docs.rs/securememory)