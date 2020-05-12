#!/usr/bin/env bash
set -e

export AWS_ACCESS_KEY_ID="dummy_key"
export AWS_SECRET_ACCESS_KEY="dummy_secret"
export AWS_DEFAULT_REGION="us-west-2"
# For DynamoDB client builder
export AWS_REGION="us-west-2"
export DISABLE_TESTCONTAINERS="true"
