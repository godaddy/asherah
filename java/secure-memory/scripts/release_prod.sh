#!/usr/bin/env bash
set -e

BASE_VERSION=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.version)
ARTIFACT_NAME=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.artifactId)
TAG=`echo java/${ARTIFACT_NAME}/v${BASE_VERSION}`

RESULT=$(git tag -l ${TAG})
if [[ "$RESULT" != ${TAG} ]]; then
    echo ${PRIVATE_GPG_KEY} | base64 --decode | gpg --batch --no-tty --import --yes
    echo "Release prod artifact"
    mvn -DskipTests -s ../../.circleci/settings.xml deploy -Prelease

    # Create tag
    git tag -f ${TAG} ${CIRCLE_SHA1}
    git push origin ${TAG}
else
    echo "Module is already deployed and tagged"
fi
