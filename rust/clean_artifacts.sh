#!/bin/bash
# Script to clean all Rust build artifacts

set -e

cd "$(dirname "$0")"
echo "Cleaning Rust build artifacts..."

# Remove all target directories
find . -type d -name "target" -exec rm -rf {} \; 2>/dev/null || true

# Remove all debug directories
find . -type d -name "debug" -exec rm -rf {} \; 2>/dev/null || true

# Remove all release directories
find . -type d -name "release" -exec rm -rf {} \; 2>/dev/null || true

# Remove other build artifacts
find . -type d -name ".fingerprint" -exec rm -rf {} \; 2>/dev/null || true
find . -type d -name "incremental" -exec rm -rf {} \; 2>/dev/null || true
find . -type d -name "build" -exec rm -rf {} \; 2>/dev/null || true

# Remove object files
find . -name "*.o" -type f -delete 2>/dev/null || true
find . -name "*.so" -type f -delete 2>/dev/null || true
find . -name "*.dylib" -type f -delete 2>/dev/null || true
find . -name "*.dll" -type f -delete 2>/dev/null || true
find . -name "*.rlib" -type f -delete 2>/dev/null || true
find . -name "*.rmeta" -type f -delete 2>/dev/null || true

echo "All Rust build artifacts have been cleaned."