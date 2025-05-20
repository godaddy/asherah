#!/usr/bin/env bash
set -e

# Run integration tests specifically
cargo test --manifest-path Cargo.toml --test integration_tests --features integration_tests -- --nocapture