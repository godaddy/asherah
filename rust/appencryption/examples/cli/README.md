# Asherah CLI Example

This is a command-line application that demonstrates how to use the Asherah Rust library for application-level encryption. The application supports various metastore backends, different KMS options, and configurable parameters for performance testing.

## Building

To build the application:

```bash
cd /path/to/asherah/rust/appencryption/examples/cli
cargo build --release
```

## Running

### Basic Usage

Run with in-memory metastore and static KMS (for testing):

```bash
cargo run --release -- --count 100 --sessions 5
```

### Using Different Metastores

#### MySQL Metastore

```bash
cargo run --release -- --metastore mysql --mysql-url "mysql://user:password@localhost:3306/database"
```

#### PostgreSQL Metastore

```bash
cargo run --release -- --metastore postgres --postgres-url "postgres://user:password@localhost:5432/database"
```

#### DynamoDB Metastore

```bash
cargo run --release -- --metastore dynamodb --dynamodb-table "encryption_key"
```

Make sure AWS credentials are properly configured in your environment or use the AWS_* environment variables.

### Using AWS KMS

```bash
cargo run --release -- --kms aws --region us-west-2 --key-id "alias/your-key-alias-or-arn"
```

### Performance Testing

Run a performance test with specific settings:

```bash
cargo run --release -- --count 1000 --sessions 20 --iterations 3 --metrics
```

Run the test for a specific duration:

```bash
cargo run --release -- --duration 1m --sessions 10 --metrics
```

### Caching Configuration

Configure caching behavior:

```bash
cargo run --release -- --session-cache --session-cache-size 100 --session-cache-expiry 3600
```

Disable caching:

```bash
cargo run --release -- --no-cache
```

### Key Rotation and Expiration

Configure key expiration:

```bash
cargo run --release -- --expire-after 86400 --check-interval 3600
```

### Utility Commands

Generate a random encryption key:

```bash
cargo run --release -- generate-key
```

Generate test data in JSON format:

```bash
cargo run --release -- generate-data --count 5
```

## Full Options List

```
Usage: asherah-cli [OPTIONS] [COMMAND]

Commands:
  generate-key   Generate a new random key and print it
  generate-data  Generate random test data
  help           Print this message or the help of the given subcommand(s)

Options:
  -c, --count <COUNT>                      Number of operations to perform per session [default: 1000]
  -i, --iterations <ITERATIONS>            Number of iterations to run [default: 1]
  -s, --sessions <SESSIONS>                Number of concurrent sessions to use [default: 20]
  -v, --verbose                            Enable verbose logging
  -P, --progress                           Enable progress output
  -r, --results                            Print encryption/decryption results
  -m, --metrics                            Enable metrics collection and output
  -d, --duration <DURATION>                Duration to run the test (e.g. "10s", "1m")
      --service <SERVICE>                  Service name for key hierarchy [default: exampleService]
      --product <PRODUCT>                  Product name for key hierarchy [default: productId]
      --partition <PARTITION>              Partition ID to use (defaults to random if not specified)
      --session-cache                      Enable the session cache
      --session-cache-size <SESSION_CACHE_SIZE>
                                           Session cache size
      --session-cache-expiry <SESSION_CACHE_EXPIRY>
                                           Session cache expiry in seconds
      --no-cache                           Disable caching
      --expire-after <EXPIRE_AFTER>        Key expiry time in seconds
      --check-interval <CHECK_INTERVAL>    Key check interval in seconds
      --shared-ik-cache                    Enable shared intermediate key cache
      --ik-cache-size <IK_CACHE_SIZE>      Intermediate key cache size
      --sk-cache-size <SK_CACHE_SIZE>      System key cache size
      --metastore <METASTORE>              Metastore type to use [default: memory] [possible values: memory, mysql, postgres, dynamodb]
      --kms <KMS>                          KMS type to use [default: static] [possible values: static, aws]
      --region <REGION>                    AWS region for AWS KMS
      --key-id <KEY_ID>                    AWS KMS key ID
      --mysql-url <MYSQL_URL>              MySQL connection string (mysql://user:pass@host:port/db)
      --postgres-url <POSTGRES_URL>        PostgreSQL connection string (postgres://user:pass@host:port/db)
      --dynamodb-table <DYNAMODB_TABLE>    DynamoDB table name [default: encryption_key]
      --truncate                           Truncate (delete all keys) before running
  -h, --help                               Print help
  -V, --version                            Print version
```

## Architecture

This example demonstrates:

1. Setting up the Asherah encryption library with various configurations
2. Using different metastore backends (in-memory, MySQL, PostgreSQL, DynamoDB)
3. Using different KMS implementations (static, AWS)
4. Managing encryption sessions
5. Performance testing and metrics collection
6. Concurrent operations with multiple sessions
7. Proper resource cleanup

The application follows Rust best practices for error handling, async/await patterns, and memory management.