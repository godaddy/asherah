#!/usr/bin/env bash
set -e

# Run clippy for linting with additional checks
cargo clippy --all-targets --all-features -- -D warnings