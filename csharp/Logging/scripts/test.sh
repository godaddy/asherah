#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit from coverage.
dotnet test --configuration Release --logger "trx" --no-build /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover
