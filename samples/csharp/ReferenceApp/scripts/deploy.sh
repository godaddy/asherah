#!/usr/bin/env bash
set -e

export NUGET_SOURCE=$1
dotnet pack -c Release --no-build

FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
find . -name *${FULL_VERSION}.nupkg  | xargs -L1 -I '{}' dotnet nuget push {} -s $NUGET_SOURCE

