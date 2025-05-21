#!/bin/bash
set -e

# Script to build the Asherah Rust reference application

# Determine script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "Building Asherah Rust reference application..."
cargo build --release

echo "Build complete. Binary located at: target/release/asherah-reference-app"