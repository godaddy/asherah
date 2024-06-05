#!/usr/bin/env bash

curl -sSfL https://raw.githubusercontent.com/golangci/golangci-lint/master/install.sh | sh -s v1.59.0

./bin/golangci-lint --version

./bin/golangci-lint run --config .golangci.yml
