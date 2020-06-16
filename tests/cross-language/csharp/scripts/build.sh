#!/usr/bin/env bash
set -e

# Workaround for https://github.com/SpecFlowOSS/SpecFlow/issues/1912#issue-583000545
export MSBUILDSINGLELOADCONTEXT=1
dotnet build --configuration Release --no-restore
