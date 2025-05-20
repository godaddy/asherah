#!/bin/bash
set -e

# Script to deploy the Asherah Rust Lambda example to AWS

# Determine script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

# Configuration
BUCKET_NAME="asherah-lambda-deployment-$(date +%s)"
FUNCTION_NAME="asherah-lambda-rust"
REGION="${AWS_REGION:-us-west-2}"
ROLE_NAME="asherah-lambda-execution-role"

echo "Creating S3 bucket for deployment..."
aws s3 mb s3://$BUCKET_NAME --region $REGION

echo "Creating IAM role for Lambda execution..."
ROLE_ARN=$(aws iam create-role \
  --role-name $ROLE_NAME \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Service": "lambda.amazonaws.com"
        },
        "Action": "sts:AssumeRole"
      }
    ]
  }' \
  --query 'Role.Arn' \
  --output text)

echo "Attaching policies to IAM role..."
aws iam attach-role-policy \
  --role-name $ROLE_NAME \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole

aws iam attach-role-policy \
  --role-name $ROLE_NAME \
  --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess

aws iam attach-role-policy \
  --role-name $ROLE_NAME \
  --policy-arn arn:aws:iam::aws:policy/AWSKeyManagementServicePowerUser

# Wait for the role to propagate
echo "Waiting for role to propagate..."
sleep 10

# Upload the Lambda package to S3
echo "Uploading Lambda package to S3..."
aws s3 cp target/lambda/lambda.zip s3://$BUCKET_NAME/

# Create the Lambda function
echo "Creating Lambda function..."
aws lambda create-function \
  --function-name $FUNCTION_NAME \
  --runtime provided.al2 \
  --role $ROLE_ARN \
  --handler bootstrap \
  --code S3Bucket=$BUCKET_NAME,S3Key=lambda.zip \
  --description "Asherah encryption in AWS Lambda (Rust)" \
  --timeout 30 \
  --memory-size 512 \
  --environment "Variables={ASHERAH_METASTORE_TABLE_NAME=encryption_key}" \
  --region $REGION

echo "Lambda function deployment complete."
echo "Function ARN: $(aws lambda get-function --function-name $FUNCTION_NAME --query 'Configuration.FunctionArn' --output text)"

echo "IMPORTANT: You must set the ASHERAH_KMS_KEY_ARN environment variable to your KMS key ARN before using the function."
echo "Example: aws lambda update-function-configuration --function-name $FUNCTION_NAME --environment \"Variables={ASHERAH_METASTORE_TABLE_NAME=encryption_key,ASHERAH_KMS_KEY_ARN=arn:aws:kms:$REGION:ACCOUNT_ID:key/KEY_ID}\""