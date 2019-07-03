#!/usr/bin/env bash
set -e

# Hacky work-around to support pulling updated alpha/snapshot builds
dotnet nuget locals global-packages --clear && mkdir -p $HOME/.nuget/packages

git clean -xfd
dotnet clean --configuration Release
dotnet restore