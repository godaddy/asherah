#!/usr/bin/env bash

set -e

mvn test jacoco:report
