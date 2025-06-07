#!/usr/bin/env bash
set -e

# Prepare for release
if [[ -z "${CRATE_VERSION}" ]]; then
  echo "Error: CRATE_VERSION environment variable must be set"
  exit 1
fi

# Update version in Cargo.toml
sed -i.bak "s/^version = \".*\"/version = \"${CRATE_VERSION}\"/" Cargo.toml
rm Cargo.toml.bak

# Build in release mode
cargo build --release

# Package the crate
cargo package --allow-dirty

# If PUBLISH=true is set, publish to crates.io
if [[ "${PUBLISH}" == "true" ]]; then
  # Use token if provided
  if [[ -n "${CARGO_REGISTRY_TOKEN}" ]]; then
    cargo publish --allow-dirty
  else
    echo "Warning: CARGO_REGISTRY_TOKEN not set, skipping publishing"
  fi
fi