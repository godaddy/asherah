#!/usr/bin/env bash
set -e

# dump the current resource limits
ulimit -a

# Looks like CGO has to be enabled for -race if we're using go modules
# Let benchmark failures fail the build since the plot conversion just renders empty results

# The intent of these tests is to spin up a number of goroutines then attempt to "wreak havoc"
# by closing sessions while work is in progress
CGO_ENABLED=1 go test ./... -race -run=Bench -bench=. -v --tags=race_tests -cpu=12 | tee benchmark.out && test ${PIPESTATUS[0]} -eq 0
