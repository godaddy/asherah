#!/bin/bash
set -e

# Move to the directory containing this script
cd "$(dirname "$0")/.."

# Run the encrypt and decrypt tests
cargo test --test encrypt
cargo test --test decrypt