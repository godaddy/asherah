#!/usr/bin/env bash
set -e

BASE_VERSION=$(cat .versionfile)
ARTIFACT_NAME='securememory'
TAG=`echo go/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG}  ]]; then
    # Create tag
    echo "Releasing ${ARTIFACT_NAME} artifact"
    git tag -f ${TAG} ${CIRCLE_SHA1}
    ssh-agent sh -c 'ssh-add ~/.ssh/id_rsa_git; git push origin --tags'
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
