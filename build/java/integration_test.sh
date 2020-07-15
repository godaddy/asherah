#!/usr/bin/env bash
set -e

mvn verify -Dskip.surefire.tests
