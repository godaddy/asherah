#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit and test projects from coverage.
# Using the FullyQualifiedName filter to run the unit tests only
dotnet test --filter FullyQualifiedName~AppEncryption.Tests --configuration Release --test-adapter-path:. --logger:"junit;LogFilePath=test-result.xml" /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.IntegrationTests]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover
