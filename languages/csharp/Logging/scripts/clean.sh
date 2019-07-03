#!/usr/bin/env bash
set -e 

git clean -xfd
dotnet clean --configuration Release
dotnet restore