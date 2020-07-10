#!/usr/bin/env bash

set -e

BASE_VERSION=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.version)
ARTIFACT_NAME=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.artifactId)
TAG=`echo java/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    echo ${PRIVATE_GPG_KEY} | base64 --decode | gpg --batch --no-tty --import --yes
    echo "Releasing ${ARTIFACT_NAME} artifact"
    mvn -DskipTests -s ../../.circleci/settings.xml deploy -Prelease

    # Create tag
    git tag -f ${TAG} ${CIRCLE_SHA1}
    ssh-agent sh -c 'ssh-add ~/.ssh/id_rsa_git; git push origin --tags'
    echo "Created tag ${TAG}"
else
    echo "${TAG} exists for ${ARTIFACT_NAME} v${BASE_VERSION}"
fi
