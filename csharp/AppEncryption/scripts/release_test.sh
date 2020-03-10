#!/usr/bin/env bash
set -e

REPO=`git remote -v | grep origin | grep fetch | awk '{print $2}'`
FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
BASE_VERSION=`echo $FULL_VERSION | cut -f1 -d'-'`
echo $BASE_VERSION
BRANCH="release-${BASE_VERSION}"

return_code=$(git ls-remote --exit-code --heads ${REPO} ${BRANCH} > /dev/null; echo $?)
if [ "$return_code" == "2" ]; then
   # No branch exists, so this is the first RC for this version
   NEW_VERSION="${BASE_VERSION}-rc1"

   # Bump master
   git checkout master
   MINOR_VERSION=`echo $BASE_VERSION | cut -f2 -d.`
   NEW_BASE_VERSION=`echo $BASE_VERSION | cut -f1 -d.`.$((${MINOR_VERSION}+1)).0
   setversion "${NEW_BASE_VERSION}-alpha" Directory.Build.props
   git add Directory.Build.props
   git commit -m "Master version bump to ${NEW_BASE_VERSION}-alpha"
   git push origin master

   # Branch
   git checkout -b "${BRANCH}"
else
   # Checkout the branch, and figure out our new RC version
   git fetch $REPO ${BRANCH}
   git checkout "${BRANCH}"
   FULL_VERSION=$(xmllint --xpath "//Project/PropertyGroup/Version/text()" Directory.Build.props)
   RC=$(echo "$FULL_VERSION" | cut -f2 -d'-' | tr -d [:alpha:])
   NEW_VERSION="${BASE_VERSION}-rc$((${RC}+1))"
fi

# Bump branch and tag
setversion "${NEW_VERSION}" Directory.Build.props
git add Directory.Build.props
git commit -m "Release candidate version bump to ${NEW_VERSION}"
TAG="v${NEW_VERSION}"
git tag -f ${TAG}
git push origin "${BRANCH}"
git push origin ${TAG}
echo $NEW_VERSION