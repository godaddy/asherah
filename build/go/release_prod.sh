#!/usr/bin/env bash
set -e

BASE_VERSION=$(cat .versionfile)
ARTIFACT_NAME=$(go mod edit -json | jq -r '.Module.Path'  | sed  's/github.com\/godaddy\/asherah\/go\///')
TAG=`echo go/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG}  ]]; then
    # Create tag
    PARENT_COMMIT=$(git rev-parse "${GITHUB_SHA}^2")
    echo "Releasing: ${ARTIFACT_NAME} v${BASE_VERSION}"
    echo "Creating new tag: ${TAG}, SHA: ${PARENT_COMMIT}"
    git tag -f ${TAG} ${PARENT_COMMIT}
    git push origin --tags
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
