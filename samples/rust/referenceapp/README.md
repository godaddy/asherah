# Asherah Rust Reference Application

This reference application demonstrates how to use the Asherah encryption library in a Rust application. It provides similar functionality to the Go reference app but uses Rust idioms and best practices.

## Features

- Configurable key management service (Static or AWS KMS)
- Configurable metastore backends (In-memory, MySQL, PostgreSQL, or DynamoDB)
- Command-line options for encryption and decryption
- Supports various configuration parameters

## Building

```bash
cargo build --release
```

## Usage

### Basic Usage

```bash
# Encrypt a payload and display the Data Row Record (DRR)
cargo run -- --payload "my secret data"

# Decrypt a previously generated DRR
cargo run -- --drr "base64encodedDRR..."
```

### Metastore Options

```bash
# Use in-memory metastore (default)
cargo run -- --metastore memory

# Use MySQL metastore
cargo run -- --metastore rdbms --conn "mysql://user:pass@localhost:3306/dbname"

# Use DynamoDB metastore
cargo run -- --metastore dynamodb --dynamodb-region us-west-2 --dynamodb-table-name my_encryption_keys
```

### KMS Options

```bash
# Use static KMS (default, for testing only)
cargo run -- --kms-type static

# Use AWS KMS
cargo run -- --kms-type aws --preferred-region us-west-2 --region-arn-tuples "us-west-2=arn:aws:kms:us-west-2:..."
```

### Verbose Output

```bash
cargo run -- --verbose
```

## Command Line Options

Run with `--help` to see all available options:

```bash
cargo run -- --help
```

## Examples

### Encrypt and Decrypt

```bash
# Encrypt data
cargo run -- --payload "sensitive information" --metastore memory --kms-type static --verbose

# Decrypt data (using the DRR from the output above)
cargo run -- --drr "BASE64_ENCODED_DRR" --metastore memory --kms-type static --verbose
```

### Use with AWS KMS and DynamoDB

```bash
cargo run -- \
  --payload "sensitive information" \
  --metastore dynamodb \
  --dynamodb-region us-west-2 \
  --dynamodb-table-name encryption_keys \
  --kms-type aws \
  --preferred-region us-west-2 \
  --region-arn-tuples "us-west-2=arn:aws:kms:us-west-2:ACCOUNT_ID:key/KEY_ID"
```

## Important Notes

1. The static KMS option is for testing only and should not be used in production.
2. When using AWS KMS, you need to have the appropriate AWS credentials configured.
3. The in-memory metastore does not persist keys and should only be used for testing.