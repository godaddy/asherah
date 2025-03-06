#!/usr/bin/env bash
set -e

CSPROJ_FILE=$(find . -name '*AppEncryption.csproj' -o -name '*SecureMemory.csproj' -o -name '*Logging.csproj')
BASE_VERSION=$(grep -o '<Version>.*<.*>' Directory.Build.props | sed 's/<Version>\(.*\)<.*>/\1/')
ARTIFACT_NAME=$(grep -o '<Title>.*<.*>' ${CSPROJ_FILE} | sed 's/<Title>\(.*\)<.*>/\1/')
TAG=`echo csharp/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    echo "Releasing: ${ARTIFACT_NAME} v${BASE_VERSION}"
    dotnet pack -c Release
    find . -name *${BASE_VERSION}.nupkg  | xargs -L1 -I '{}' dotnet nuget push {} -k ${NUGET_KEY} -s ${NUGET_SOURCE}

    # Create tag
    PARENT_COMMIT=$(git rev-parse "${GITHUB_SHA}^2")
    echo "Creating new tag: ${TAG}, SHA: ${PARENT_COMMIT}"
    git tag -f ${TAG} ${PARENT_COMMIT}
    git push origin --tags
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
