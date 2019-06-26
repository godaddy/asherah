#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit from coverage.
dotnet test --configuration Release --no-build /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" /p:CoverletOutputFormat=cobertura