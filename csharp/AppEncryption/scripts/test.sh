#!/usr/bin/env bash
set -e

# Have to explicitly exclude xunit and test projects from coverage.
for framework in net10.0 net11.0; do
  dotnet test AppEncryption.Tests/AppEncryption.Tests.csproj \
    --configuration Release --framework "$framework" --test-adapter-path:. \
    --logger:"junit;LogFilePath=test-result-${framework}.xml" --no-build \
    /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.IntegrationTests]*,[*.Tests]*\" \
    /p:CoverletOutputFormat=opencover /p:CoverletOutput="coverage/${framework}/" "$@"
done
