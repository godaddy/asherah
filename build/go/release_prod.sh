#!/usr/bin/env bash
set -e

BASE_VERSION=$(cat .versionfile)
ARTIFACT_NAME=$(go mod edit -json | jq -r '.Module.Path'  | sed  's/github.com\/godaddy\/asherah\/go\///')
TAG=`echo go/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG}  ]]; then
    # Create tag
    echo "Releasing ${ARTIFACT_NAME} artifact"
    git tag -f ${TAG} ${GITHUB_SHA}
    git push origin --tags
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
