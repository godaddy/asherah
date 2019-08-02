#!/usr/bin/env bash
set -e

FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
TIME_STAMP=$(date "+%Y%m%d%S")
PACKAGE_VERSION=${FULL_VERSION}.${TIME_STAMP}

dotnet pack -c Release -p:PackageVersion=${PACKAGE_VERSION}
find . -name *${PACKAGE_VERSION}.nupkg -exec dotnet nuget push {} -s local \;
