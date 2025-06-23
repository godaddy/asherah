# Asherah AWS Lambda Integration (Rust)

This example demonstrates how to use Asherah encryption in an AWS Lambda function written in Rust. The example provides functionality to encrypt and decrypt data using AWS KMS for key management and DynamoDB for key persistence.

## Features

- Serverless encryption and decryption using AWS Lambda
- Secure memory handling with proper resource limits
- Performance metrics collection
- Panic recovery with retry logic for memory-related errors
- Proper session lifecycle management

## Prerequisites

- Rust 1.70+ with Cargo
- AWS CLI configured with appropriate permissions
- AWS SAM CLI (for deployment)

## Building the Lambda Function

```bash
cargo build --release --target x86_64-unknown-linux-musl
```

This builds a statically linked binary suitable for AWS Lambda. For custom build configurations, check the `lambda.config.toml` file.

## Deployment

### Using AWS SAM

1. Build the Lambda package:
   ```bash
   sam build
   ```

2. Deploy the Lambda function:
   ```bash
   sam deploy --guided
   ```

### Manual Deployment

1. Create a deployment package:
   ```bash
   scripts/1-create-package.sh
   ```

2. Create the S3 bucket and deploy:
   ```bash
   scripts/2-deploy.sh
   ```

## Required Environment Variables

The Lambda function requires the following environment variables to be set:

- `AWS_REGION`: The AWS region where your resources are deployed
- `ASHERAH_METASTORE_TABLE_NAME`: The name of the DynamoDB table for key storage
- `ASHERAH_KMS_KEY_ARN`: The ARN of the KMS key used for master key operations

## Lambda Resource Configuration

For the Lambda function to work properly with secure memory, you need to configure:

1. Memory size: At least 512MB recommended
2. Timeout: At least 10 seconds
3. Resource-based policy for KMS and DynamoDB access

## Usage

### Input Format

The Lambda function accepts requests in the following JSON format:

#### For Encryption:

```json
{
  "name": "document-1",
  "partition": "user123",
  "payload": "SGVsbG8gV29ybGQ="  // Base64-encoded data to encrypt
}
```

#### For Decryption:

```json
{
  "name": "document-1",
  "partition": "user123",
  "drr": {
    "key": "...",
    "data": "..."
  }
}
```

### Response Format

Responses are returned in the following format:

#### For Encryption:

```json
{
  "drr": {
    "key": "...",
    "data": "..."
  },
  "metrics": {
    "encryption_time_ms": 12.5,
    "session_creation_ms": 45.2
  }
}
```

#### For Decryption:

```json
{
  "plain_text": "Hello World",
  "metrics": {
    "decryption_time_ms": 10.8,
    "session_creation_ms": 42.1
  }
}
```

## Testing

You can test the deployed function using the provided script:

```bash
scripts/3-invoke.sh
```

## Performance Considerations

1. **Cold starts**: The first invocation will be slower due to the Lambda cold start and Asherah initialization
2. **Memory limits**: Increase Lambda memory for better performance
3. **Connection pooling**: The Lambda maintains DynamoDB connections between invocations when possible

## Security Considerations

1. Secure memory is used to protect sensitive data in memory
2. The Lambda has safeguards to recover from memory limit errors
3. Key expiration policies are enforced per the Asherah crypto policy
4. The function should be deployed with least-privilege IAM permissions