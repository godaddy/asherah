#!/usr/bin/env bash
set -e

dotnet pack -c Release --version-suffix alpha-$(date "+%Y%m%d")
find . -name *.nupkg -exec dotnet nuget push {} -s local \;
