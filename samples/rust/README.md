# Asherah Rust Examples

This directory contains sample applications and integrations that demonstrate how to use the Asherah encryption library in Rust applications.

## Examples

### Reference Application

The [referenceapp](./referenceapp) directory contains a command-line application that demonstrates the basic usage of Asherah for encryption and decryption. It supports various metastore backends (in-memory, MySQL, PostgreSQL, DynamoDB) and key management services (static, AWS KMS).

### AWS Lambda Integration

The [aws/lambda](./aws/lambda) directory contains an example of integrating Asherah with AWS Lambda for serverless encryption and decryption. It includes deployment scripts and AWS SAM template for easy deployment.

## Features Demonstrated

These examples demonstrate the following Asherah features:

1. **Basic Encryption/Decryption**: How to encrypt and decrypt data using Asherah's envelope encryption
2. **Persistent Key Storage**: How to store encryption keys in various backends (DynamoDB, MySQL, PostgreSQL)
3. **Key Management**: How to integrate with AWS KMS for key management
4. **Secure Memory Handling**: How to use Asherah's secure memory features to protect sensitive data in memory
5. **Configuration Options**: How to configure Asherah for different use cases
6. **Performance Optimization**: Best practices for optimizing Asherah performance
7. **Error Handling**: Proper error handling and recovery techniques

## Getting Started

Each example directory contains its own README.md with specific instructions for building and running the example.

### Prerequisites

- Rust 1.70 or higher
- Cargo package manager
- For AWS examples: AWS CLI and appropriate AWS credentials

### Building the Examples

Each example can be built using Cargo:

```bash
cd referenceapp
cargo build --release
```

## Documentation

For more information about Asherah, refer to the [main Asherah documentation](../../docs/).