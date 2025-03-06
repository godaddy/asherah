#!/usr/bin/env bash

set -e

BASE_VERSION=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.version)
ARTIFACT_NAME=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.artifactId)
TAG=`echo java/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    echo "Releasing: ${ARTIFACT_NAME} v${BASE_VERSION}"
    mvn -DskipTests deploy -Prelease

    # Create tag
    PARENT_COMMIT=$(git rev-parse "${GITHUB_SHA}^2")
    echo "Creating new tag: ${TAG}, SHA: ${PARENT_COMMIT}"
    git tag -f ${TAG} ${PARENT_COMMIT}
    git push origin --tags
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
