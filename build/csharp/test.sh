#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit from coverage.
dotnet test --configuration Release --test-adapter-path:. --logger:"junit;LogFilePath=test-result.xml" /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover
