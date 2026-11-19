#!/usr/bin/env bash
set -e

for framework in net10.0 net11.0; do
  dotnet test SecureMemory.Tests/SecureMemory.Tests.csproj \
    --configuration Release --framework "$framework" --test-adapter-path:. \
    --logger:"junit;LogFilePath=test-result-${framework}.xml" --no-build \
    /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.Tests]*\" \
    /p:CoverletOutputFormat=opencover /p:CoverletOutput="coverage/${framework}/"
done
