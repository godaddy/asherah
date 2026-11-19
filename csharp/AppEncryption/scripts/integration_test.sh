#!/usr/bin/env bash
set -e

# Run integration tests without recording coverage.
for framework in net10.0 net11.0; do
  dotnet test AppEncryption.IntegrationTests/AppEncryption.IntegrationTests.csproj \
    --filter FullyQualifiedName~AppEncryption.IntegrationTests --configuration Release \
    --framework "$framework" --logger "trx;LogFileName=integration-${framework}.trx" --no-build "$@"
done
