#!/usr/bin/env bash
#Using https://github.com/gotestyourself/gotestsum for reference

# Note the use of `-p 1` is required to prevent multiple test packages from running in
# parallel (default), ensuring access to any shared resource (e.g., dynamodb-local)
# is serialized.
gotestsum -f testname --junitfile junit_integration_results.xml -- -p 1 -race -coverprofile coverage.out -v `go list ./integrationtest/... | grep -v traces`
