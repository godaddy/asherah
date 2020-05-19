#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit and test projects from coverage.
# Using the FullyQualifiedName filter to run the unit tests only
dotnet test --filter FullyQualifiedName~AppEncryption.Tests --configuration Release --logger "trx" --no-build /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.IntegrationTests]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover
