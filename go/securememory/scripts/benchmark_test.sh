#!/usr/bin/env bash
set -e

# https://www.asciiarmor.com/post/99010893761/jenkins-now-with-more-gopher used as reference

# Looks like CGO has to be enabled for -race if we're using go modules
# Let benchmark failures fail the build since the plot conversion just renders empty results
CGO_ENABLED=1 go test ./... -race -run=Bench -bench=. -benchtime=5s -v | tee benchmark.out && test ${PIPESTATUS[0]} -eq 0
