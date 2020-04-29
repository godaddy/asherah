#!/usr/bin/env bash
set -e

BASE_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
ARTIFACT_NAME=$(xmllint --xpath "//package/metadata/title/text()" AppEncryption/AppEncryption.nuspec)
TAG=`echo csharp/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    dotnet pack -c Release --no-build
    echo "Release prod artifact"
    find . -name *${BASE_VERSION}.nupkg  | xargs -L1 -I '{}' dotnet nuget push {} -k ${NUGET_KEY} -s ${NUGET_SOURCE}

    # Create tag
    git tag -f ${TAG} ${CIRCLE_SHA1}
    git push origin ${TAG}
else
    echo "Module is already deployed and tagged"
fi
