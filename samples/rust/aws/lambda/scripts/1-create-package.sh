#!/bin/bash
set -e

# Script to create a Lambda deployment package for the Asherah Rust Lambda example

# Determine script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "Building Asherah Rust Lambda example for AWS Lambda..."

# Use cargo lambda to build if available
if command -v cargo-lambda &> /dev/null; then
    cargo lambda build --release
else
    # Fall back to standard build with cross-compilation for Linux
    # Note: This requires the musl target to be installed: 
    # rustup target add x86_64-unknown-linux-musl
    cargo build --release --target x86_64-unknown-linux-musl
    
    # Create the deployment package
    mkdir -p target/lambda
    cp target/x86_64-unknown-linux-musl/release/asherah-lambda target/lambda/bootstrap
    cd target/lambda
    zip -j lambda.zip bootstrap
    cd "$PROJECT_DIR"
    
    echo "Created deployment package at target/lambda/lambda.zip"
fi

echo "Build complete."