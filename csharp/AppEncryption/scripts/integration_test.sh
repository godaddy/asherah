#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit and test projects from coverage.
# Using the FullyQualifiedName filter to run the integration tests only. Do not record coverage
dotnet test --filter FullyQualifiedName~AppEncryption.IntegrationTests --configuration Release --logger "trx" --no-build
