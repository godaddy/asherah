#!/usr/bin/env bash
#Using https://github.com/gotestyourself/gotestsum for reference

gotestsum -f testname --junitfile junit_results.xml -- -race -coverprofile coverage.out -v ./...
