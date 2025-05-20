# Asherah Cross-Language Testing Framework - Rust

This directory contains the Rust implementation of the Asherah cross-language testing framework. 
It allows testing compatibility between the Rust, Java, C#, and Go implementations of Asherah
to ensure that each language implementation can encrypt and decrypt data that's compatible with
all other implementations.

## Requirements

- Rust (1.70.0 or newer)
- MySQL database (for metastore)
- Environment variables:
  - `TEST_DB_USER`: Database username (default: "test")
  - `TEST_DB_PASSWORD`: Database password (default: "test")
  - `TEST_DB_HOSTNAME`: Database hostname (default: "localhost")
  - `TEST_DB_PORT`: Database port (default: "3306")
  - `TEST_DB_NAME`: Database name (default: "test")

## Setup

1. Make sure you have Rust installed. If not, you can install it using [rustup](https://rustup.rs/).
2. Make sure you have a MySQL database running.
3. Set up the necessary environment variables for database connection.

## Running Tests

To run the Rust-specific tests:

```bash
cd tests/cross-language/rust
./scripts/test.sh
```

This will run both the encrypt and decrypt tests using the Cucumber feature files.

## Cross-Language Testing

The Rust implementation fully participates in the cross-language testing framework:

1. Run the encrypt test across all languages:
```bash
cd tests/cross-language
./scripts/encrypt_all.sh
```

This script:
- Encrypts a payload using each language implementation
- Writes the encrypted data to language-specific files

2. Run the decrypt test across all languages:
```bash
cd tests/cross-language
./scripts/decrypt_all.sh
```

This script:
- Has each language implementation decrypt data encrypted by all other languages
- Verifies that the decrypted data matches the original

This ensures that all language implementations can correctly decrypt data encrypted 
by any other language implementation, demonstrating full interoperability.

## Implementation Details

The Rust implementation is fully compatible with other language implementations:
- Uses the same crypto policy settings (key expiry, revocation checks) 
- Uses identical service/product IDs and partition keys
- Creates compatible encrypted data formats
- Properly implements the cross-language test framework

## Structure

- `src/lib.rs`: Core library containing encryption/decryption functionality
- `src/constants.rs`: Constants used in the testing framework (matching other languages)
- `tests/encrypt.rs`: Cucumber-based test for encryption
- `tests/decrypt.rs`: Cucumber-based test for decryption
- `scripts/`: Helper scripts for building and testing

## Troubleshooting

If you encounter issues:
1. Verify your MySQL database is running and accessible
2. Check that environment variables are set correctly
3. Ensure your Rust version is up to date
4. Check for compatibility issues in constants between languages