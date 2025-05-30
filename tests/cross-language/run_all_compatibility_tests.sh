#!/bin/bash
# Cross-language compatibility test runner for Asherah
# This script runs all language implementations and verifies cross-language compatibility

set -e

# Create test data directory if it doesn't exist
mkdir -p test-data

echo "=========================================="
echo "Asherah Cross-Language Compatibility Tests"
echo "=========================================="
echo ""

# Function to check if a language is available
is_available() {
    case $1 in
        rust)
            command -v cargo > /dev/null
            return $?
            ;;
        go)
            command -v go > /dev/null
            return $?
            ;;
        java)
            command -v mvn > /dev/null
            return $?
            ;;
        csharp)
            command -v dotnet > /dev/null
            return $?
            ;;
        *)
            return 1
            ;;
    esac
}

# Run Rust tests if available
if is_available rust; then
    echo "Running Rust compatibility tests..."
    (cd rust && cargo test --test compatibility)
    echo "Rust tests completed."
    echo ""
else
    echo "Rust not available, skipping Rust tests."
    echo ""
fi

# Run Go tests if available
if is_available go; then
    echo "Running Go compatibility tests..."
    (cd go && go test -v ./tests/cross_language_test.go)
    echo "Go tests completed."
    echo ""
else
    echo "Go not available, skipping Go tests."
    echo ""
fi

# Run Java tests if available
if is_available java; then
    echo "Running Java compatibility tests..."
    (cd java && mvn test -Dtest=CrossLanguageTest)
    echo "Java tests completed."
    echo ""
else
    echo "Java not available, skipping Java tests."
    echo ""
fi

# Run C# tests if available
if is_available csharp; then
    echo "Running C# compatibility tests..."
    (cd csharp && dotnet test --filter 'CrossLanguageTests')
    echo "C# tests completed."
    echo ""
else
    echo "C# not available, skipping C# tests."
    echo ""
fi

# Check which test files were created
echo "Checking test results..."
echo ""

if [ -f "test-data/rust-encrypted.bin" ]; then
    echo "✅ Rust encryption test completed successfully."
else
    echo "❌ Rust encryption test did not generate output file."
fi

if [ -f "test-data/go-encrypted.bin" ]; then
    echo "✅ Go encryption test completed successfully."
else
    echo "❌ Go encryption test did not generate output file."
fi

if [ -f "test-data/java-encrypted.bin" ]; then
    echo "✅ Java encryption test completed successfully."
else
    echo "❌ Java encryption test did not generate output file."
fi

if [ -f "test-data/csharp-encrypted.bin" ]; then
    echo "✅ C# encryption test completed successfully."
else
    echo "❌ C# encryption test did not generate output file."
fi

echo ""
echo "Test Summary"
echo "------------"
echo "The compatibility tests verify that each language implementation can:"
echo "1. Encrypt data in a format that other implementations can decrypt"
echo "2. Decrypt data that was encrypted by other implementations"
echo ""
echo "If all tests pass, it means all implementations are compatible with each other."
echo ""
echo "Tests completed!"