#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "=== Building Asherah Native Image Sample ==="
echo "Project directory: $PROJECT_DIR"
echo ""

# Check for GraalVM
if ! command -v native-image &> /dev/null; then
    echo "ERROR: native-image not found. Please install GraalVM 25 or later."
    echo ""
    echo "Install with Homebrew:"
    echo "  brew install --cask graalvm-jdk"
    echo ""
    echo "Or download from: https://www.graalvm.org/downloads/"
    exit 1
fi

echo "Using native-image: $(which native-image)"
native-image --version
echo ""

# Build native image
echo "Building native image..."
mvn -Pnative package -DskipTests

echo ""
echo "=== Build Complete ==="
echo "Native executable: $PROJECT_DIR/target/asherah-native"
echo ""
echo "Run with: ./target/asherah-native"

