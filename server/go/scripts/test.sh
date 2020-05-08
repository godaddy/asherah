#!/usr/bin/env bash
#Using https://github.com/gotestyourself/gotestsum for reference

# Looks like CGO has to be enabled for -race if we're using go modules
CGO_ENABLED=1 gotestsum --junitfile junit_results.xml -- -race -coverprofile coverage.out -v ./...
