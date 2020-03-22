#!/usr/bin/env bash

curl -sfL https://install.goreleaser.com/github.com/golangci/golangci-lint.sh | sh -s v1.24.0
./bin/golangci-lint run --config .golangci.yml

# golint is designed to return zero even it finds lint
# https://github.com/golang/lint/issues/65
# Hence we check if the lint command produced any output. If no output, then no errors were found
if [[ $(./bin/golangci-lint run --config .golangci.yml) ]]; then
    exit 1
else
    exit 0
fi
