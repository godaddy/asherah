#!/usr/bin/env bash
set -e

mvn failsafe:integration-test -Dskip.surefire.tests
