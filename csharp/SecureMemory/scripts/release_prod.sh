#!/usr/bin/env bash
set -e

BASE_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
ARTIFACT_NAME=$(xmllint --xpath "//Project/PropertyGroup/Title/text()" SecureMemory/SecureMemory.csproj)
TAG=`echo csharp/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    dotnet pack -c Release --no-build
    echo "Releasing ${ARTIFACT_NAME} artifact"
    find . -name *${BASE_VERSION}.nupkg  | xargs -L1 -I '{}' dotnet nuget push {} -k ${NUGET_KEY} -s ${NUGET_SOURCE}

    # Create tag
    git tag -f ${TAG} ${CIRCLE_SHA1}
    ssh-agent sh -c 'ssh-add ~/.ssh/id_rsa_git; git push origin --tags'
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
