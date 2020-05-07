#!/usr/bin/env bash
set -e

export AWS_ACCESS_KEY_ID="dummy_key"
export AWS_DEFAULT_REGION="us-west-2"
export AWS_SECRET_ACCESS_KEY="dummy_secret"
export DISABLE_TESTCONTAINERS="true"
export TEST_DB_NAME="testdb"
export TEST_DB_PASSWORD="Password123"
export TEST_DB_USER="root"
