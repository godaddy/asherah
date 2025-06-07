#!/bin/bash
set -e

# Move to the directory containing this script
cd "$(dirname "$0")/.."

# Build the Rust cross-language testing framework
cargo build