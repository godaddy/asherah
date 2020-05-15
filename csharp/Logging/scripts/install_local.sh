#!/usr/bin/env bash
set -e

FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
VERSION_SUFFIX=`echo ${FULL_VERSION} | cut -f2 -d'-'`

 if [[ "${VERSION_SUFFIX}" == "alpha" ]]; then
    TIME_STAMP=$(date "+%Y%m%d%H%M%S")
    PACKAGE_VERSION=${FULL_VERSION}.${TIME_STAMP}

    dotnet pack -c Release -p:PackageVersion=${PACKAGE_VERSION}
 else
    dotnet pack -c Release --no-build
    # Delete existing packages from local nuget to support local installation
    # This is some ugly bash magic to split package name and version tuples to pipe them properly into dotnet nuget delete.
    find . -name *.nupkg | sed -E 's/.*\///g ; s/\.nupkg//g ; s/([^0-9])\.([0-9].*)/\1 \2/g' | tr '\n' '\0' | xargs -0 -n1 -I '{}' /bin/bash -c 'dotnet nuget delete {} -s local --non-interactive || true'
 fi

find . -name *.nupkg -exec dotnet nuget push {} -s local \;
