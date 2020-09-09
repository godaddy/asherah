#!/usr/bin/env bash
set -e

# dump the current resource limits
ulimit -a

# Looks like CGO has to be enabled for -race if we're using go modules
# Let benchmark failures fail the build since the plot conversion just renders empty results
CGO_ENABLED=1 go test ./... -race -run=Bench -bench=. -benchtime=5s -v --tags=race_tests | tee benchmark.out && test ${PIPESTATUS[0]} -eq 0
