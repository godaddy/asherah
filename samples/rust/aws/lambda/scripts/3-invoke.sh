#!/bin/bash
set -e

# Script to invoke the Asherah Rust Lambda example

# Determine script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

# Configuration
FUNCTION_NAME="asherah-lambda-rust"
REGION="${AWS_REGION:-us-west-2}"

# Test encryption
echo "Testing encryption..."
RESULT=$(aws lambda invoke \
  --function-name $FUNCTION_NAME \
  --payload '{
    "name": "test-document",
    "partition": "user123",
    "payload": "SGVsbG8gQXNoZXJhaCBSdXN0IQ=="
  }' \
  --cli-binary-format raw-in-base64-out \
  --region $REGION \
  /tmp/lambda-encrypt-output.json)

echo "Encryption result: $RESULT"
echo "Output:"
cat /tmp/lambda-encrypt-output.json

# Extract the DRR for decryption test
DRR=$(jq -c '.drr' /tmp/lambda-encrypt-output.json)

# Test decryption
echo -e "\nTesting decryption..."
RESULT=$(aws lambda invoke \
  --function-name $FUNCTION_NAME \
  --payload "{
    \"name\": \"test-document\",
    \"partition\": \"user123\",
    \"drr\": $DRR
  }" \
  --cli-binary-format raw-in-base64-out \
  --region $REGION \
  /tmp/lambda-decrypt-output.json)

echo "Decryption result: $RESULT"
echo "Output:"
cat /tmp/lambda-decrypt-output.json