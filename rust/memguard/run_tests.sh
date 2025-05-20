#!/bin/bash

# Extract all test names
TESTS=$(cargo test --lib -- --list | grep "test " | awk '{print $1}')

# Run each test with a timeout
for test in $TESTS; do
    echo "=========================================="
    echo "Running $test"
    echo "=========================================="
    timeout 5 cargo test --lib $test
    RESULT=$?
    if [ $RESULT -eq 124 ]; then
        echo "TEST TIMED OUT: $test"
    elif [ $RESULT -ne 0 ]; then
        echo "TEST FAILED WITH CODE $RESULT: $test"
    else
        echo "TEST PASSED: $test"
    fi
    echo ""
done