#!/usr/bin/env bash

set -e
JAVA_AE_VERSION=$(mvn -f ../../../java/app-encryption/pom.xml -q -DforceStdout help:evaluate -Dexpression=project.version)
mvn --no-transfer-progress -Drevision=${JAVA_AE_VERSION} -U compile
