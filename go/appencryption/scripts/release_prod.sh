#!/usr/bin/env bash
set -e

BASE_VERSION=$(cat .versionfile)
ARTIFACT_NAME=$(go mod edit -json | jq -r '.Module.Path')
TAG=`echo go/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG}  ]]; then
    # Create tag
    echo "Creating a new release tag"
    git tag -f ${TAG} ${CIRCLE_SHA1}
    git push origin ${TAG}
else
    echo "Module is already tagged"
fi
