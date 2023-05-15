#!/usr/bin/env bash
set -e

mvn --no-transfer-progress verify -Dskip.surefire.tests
