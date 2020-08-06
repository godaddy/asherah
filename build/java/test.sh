#!/usr/bin/env bash

set -e

mvn surefire:test jacoco:report
