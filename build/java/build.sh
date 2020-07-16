#!/usr/bin/env bash

set -e

mvn javadoc:javadoc
mvn -U compile
