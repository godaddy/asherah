#!/usr/bin/env bash
set -e

# Run tests with coverage using cargo-tarpaulin if installed
if command -v cargo-tarpaulin &> /dev/null; then
  cargo tarpaulin --out Xml --output-dir . --manifest-path Cargo.toml
else
  # Otherwise run tests normally
  cargo test --all-features --manifest-path Cargo.toml -- --nocapture
fi