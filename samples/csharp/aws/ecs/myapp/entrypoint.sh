#!/bin/bash

# dump the resource limits for this process
ulimit -a

exec dotnet myapp.dll "$@"
