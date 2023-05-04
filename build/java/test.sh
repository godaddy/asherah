#!/usr/bin/env bash

set -e

mvn --no-transfer-progress test jacoco:report
