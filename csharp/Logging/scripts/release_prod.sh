#!/usr/bin/env bash
set -e

FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
BASE_VERSION=`echo $FULL_VERSION | cut -f1 -d'-'`
echo $BASE_VERSION
BRANCH="release-${BASE_VERSION}"
NEW_VERSION="${BASE_VERSION}"
echo $NEW_VERSION

git fetch
git checkout "${BRANCH}"
git pull origin "${BRANCH}"
setversion "${NEW_VERSION}" Directory.Build.props
git add Directory.Build.props
git commit -m "Production release version bump to ${NEW_VERSION}"
TAG="v${NEW_VERSION}"
git tag -f ${TAG}
git push origin "${BRANCH}"
git push origin ${TAG}
