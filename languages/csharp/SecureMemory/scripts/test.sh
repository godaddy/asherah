#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit from coverage. Excluding all MacOS related files
dotnet test --configuration Release --no-build /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" /p:CoverletOutputFormat=opencover /p:ExcludeByFile="../**/MacOS/**/*.cs"
