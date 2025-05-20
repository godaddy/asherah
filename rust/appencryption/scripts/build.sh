#!/usr/bin/env bash
set -e

# Build the library in release mode
cargo build --release --manifest-path Cargo.toml

# Also run a check to verify everything compiles without errors
cargo check --all-features --all-targets