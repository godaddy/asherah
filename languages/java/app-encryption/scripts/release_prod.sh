#!/usr/bin/env bash
FULL_VERSION=$(mvn -q -DforceStdout help:evaluate -Dexpression=project.version)
BASE_VERSION=`echo $FULL_VERSION | cut -f1 -d'-'`
echo $BASE_VERSION
BRANCH="release-${BASE_VERSION}"
NEW_VERSION="${BASE_VERSION}"
echo $NEW_VERSION

git fetch
git checkout "${BRANCH}"
git pull origin "${BRANCH}"
mvn versions:set versions:commit -DnewVersion="${NEW_VERSION}"
git add pom.xml
git commit -m "Production release version bump to ${NEW_VERSION}"
TAG="v${NEW_VERSION}"
git tag -f ${TAG}
git push origin "${BRANCH}"
git push origin ${TAG}
