#!/usr/bin/env bash
set -e

export NUGET_SOURCE=$1
dotnet pack -c Release --no-build
# Delete existing packages from nuget to support updating -alpha builds and idempotency for others (i.e. mimic mvn deploy semantics).
# This is some ugly bash magic to split package name and version tuples to pipe them properly into dotnet nuget delete.
find . -name *.nupkg | sed -E 's/.*\///g ; s/\.nupkg//g ; s/([^0-9])\.([0-9].*)/\1 \2/g' | tr '\n' '\0' | xargs -0 -n1 -I '{}' /bin/bash -c 'dotnet nuget delete {} -s $NUGET_SOURCE --non-interactive || true'

FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
find . -name *${FULL_VERSION}.nupkg  | xargs -L1 -I '{}' dotnet nuget push {} -s $NUGET_SOURCE
