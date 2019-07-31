#!/usr/bin/env bash

# This is a patched copy of https://github.com/iynere/compare-url/blob/master/src/commands/reconstruct.yml.
# Using this until https://github.com/iynere/compare-url/issues/25 is resolved

## VARS

# this starts as false, set to true to exit `until` loop
FOUND_BASE_COMPARE_COMMIT=false

# start iteration from the job before $CIRCLE_BUILD_NUM
JOB_NUM=$(( $CIRCLE_BUILD_NUM - 1 ))

## UTILS

extract_commit_from_job () {
  # abstract this logic out, it gets reused a few times
  # takes $1 (VCS_TYPE) & $2 (a job number)

  curl --user $CIRCLE_TOKEN: \
    https://circleci.com/api/v1.1/project/$1/$CIRCLE_PROJECT_USERNAME/$CIRCLE_PROJECT_REPONAME/$2 | \
    grep '"vcs_revision" : ' | sed -E 's/"vcs_revision" ://' | sed -E 's/[[:punct:]]//g' | sed -E 's/ //g'
}

check_if_branch_is_new () {
  # takes a single argument for VCS_TYPE
  # functionally, 'new' means: same commit for all jobs on the branch

  # assume this is true, set to false if proven otherwise
  local BRANCH_IS_NEW=true

  # grab URL endpoints for jobs on this branch
  # transform them into single-job API endpoints
  # output them to a file for subsequent iteration
  curl --user $CIRCLE_TOKEN: \
    https://circleci.com/api/v1.1/project/$1/$CIRCLE_PROJECT_USERNAME/$CIRCLE_PROJECT_REPONAME/tree/$CIRCLE_BRANCH?limit=100 | \
    grep "\"build_url\" : \"http" | sed -E 's/"build_url" : //' | \
    sed -E 's|/bb/|/api/v1.1/project/bitbucket/|' | \
    sed -E 's|/gh/|/api/v1.1/project/github/|' | \
    sed -E 's/"|,//g' | sed -E 's/ //g' \
    > API_ENDPOINTS_FOR_JOBS_ON_BRANCH

  # loop through each job to compare commit hashes
  while read line
  do
    if [[ $(curl --user $CIRCLE_TOKEN: $line | grep "\"vcs_revision\" : \"$CIRCLE_SHA1\"") ]]; then
      continue
    else
      BRANCH_IS_NEW=false
      break
    fi
  done < API_ENDPOINTS_FOR_JOBS_ON_BRANCH

  # clean up
  if [[ false == false ]]; then
    rm -f API_ENDPOINTS_FOR_JOBS_ON_BRANCH
  fi

  # true or false
  echo $BRANCH_IS_NEW
}

## SETUP

# determine VCS type, so we don't worry about it later
if [[ $(echo $CIRCLE_REPOSITORY_URL | grep github.com:$CIRCLE_PROJECT_USERNAME) ]]; then
  VCS_TYPE=github
else
  VCS_TYPE=bitbucket
fi

# check if this is a new branch, as that informs later steps
echo "checking if $CIRCLE_BRANCH is a new branch..."
echo "----------------------------------------------------------------------------------------------------"
if [[ $(check_if_branch_is_new $VCS_TYPE) == true ]]; then
  echo "----------------------------------------------------------------------------------------------------"
  echo "yes, $CIRCLE_BRANCH is new and $CIRCLE_SHA1 is its only commit"
  echo "finding most recent ancestor commit from any other branch..."
  echo "----------------------------------------------------------------------------------------------------"
  BRANCH_IS_NEW=true
else
  echo "----------------------------------------------------------------------------------------------------"
  echo "$CIRCLE_BRANCH is not a new branch, searching for its most recent previous commit..."
  echo "----------------------------------------------------------------------------------------------------"
  BRANCH_IS_NEW=false
fi

## EXECUTION

# manually iterate through previous jobs
until [[ $FOUND_BASE_COMPARE_COMMIT == true ]]
do

  # save circle api output to a temp file for reuse
  curl --user $CIRCLE_TOKEN: \
    https://circleci.com/api/v1.1/project/$VCS_TYPE/$CIRCLE_PROJECT_USERNAME/$CIRCLE_PROJECT_REPONAME/$JOB_NUM \
    > JOB_OUTPUT

  # general approach:
  # there's a couple of skip conditions to observe here—
  # roughly in order of precedence:

  # 1. is JOB_NUM part of the current workflow?
  # 2. is JOB_NUM a retry of a job from the same commit?
    # 2.5 or part of a rerun workflow from the same commit?
  # 3. is JOB_NUM from a different branch?
    # 3.5 unless this is a new branch—see below

  # edge cases:
  # 1. if $CIRCLE_SHA1 is the first commit on a new branch
    # then we need the most recent ancestor, branch-agnostic
    # 1.5 a new branch doesn't always mean a new commit

  # handling condition 3 & edge case 1:
  # check if this is a brand-new branch
  if [[ $BRANCH_IS_NEW == true ]]; then
    COMMIT_FROM_JOB_NUM=$(extract_commit_from_job $VCS_TYPE $JOB_NUM)

    # we do a similar check later on, but it needs to be here too
    # for edge case 1.5: an existing commit pushed to a new branch
    if [[ $COMMIT_FROM_JOB_NUM == $CIRCLE_SHA1 ]]; then
      JOB_NUM=$(( $JOB_NUM - 1 ))
      continue
    fi

    CIRCLE_WORKING_DIRECTORY="${CIRCLE_WORKING_DIRECTORY/#\~/$HOME}"
    cd $CIRCLE_WORKING_DIRECTORY

    # check if commit from JOB_NUM is an ancestor of $CIRCLE_SHA1
    git merge-base --is-ancestor $COMMIT_FROM_JOB_NUM $CIRCLE_SHA1
    RETURN_CODE=$?

    if [[ $RETURN_CODE == 1 ]]; then
      echo "----------------------------------------------------------------------------------------------------"
      echo "commit $COMMIT_FROM_JOB_NUM from job $JOB_NUM is not an ancestor of the current commit"
      echo "----------------------------------------------------------------------------------------------------"
      JOB_NUM=$(( $JOB_NUM - 1 ))
      continue
    elif [[ $RETURN_CODE == 0 ]]; then
      echo "----------------------------------------------------------------------------------------------------"
      echo "commit $COMMIT_FROM_JOB_NUM from job $JOB_NUM is an ancestor of the current commit"
      echo "----------------------------------------------------------------------------------------------------"
      FOUND_BASE_COMPARE_COMMIT=true
      break
    else
      echo "unknown return code $RETURN_CODE from git merge-base with base commit $COMMIT_FROM_JOB_NUM, from job $JOB_NUM"
      exit 1
    fi
  else
    # if not a new branch, find its most recent previous commit

    # by now, if none of conditions 1, 2/2.5, or 3 apply, we're done:
    # 1. make sure job isn't part of the same workflow
    if [[ ! $(grep "\"workflow_id\" : \"$CIRCLE_WORKFLOW_ID\"" JOB_OUTPUT) && \
      # 2. make sure job is not a retry of a previous job
      $(grep '"retry_of" : null' JOB_OUTPUT) && \
      # 2.5 make sure job is not from a rerun workflow (same commit)
      ! $(grep "\"vcs_revision\" : \"$CIRCLE_SHA1\"" JOB_OUTPUT) && \
      # make sure we are on the same branch as $CIRCLE_BRANCH
      # (we've already ruled out that this is a brand-new branch)
      $(grep "\"branch\" : \"$CIRCLE_BRANCH\"" JOB_OUTPUT) ]]; then

      echo "----------------------------------------------------------------------------------------------------"
      echo "success! job $JOB_NUM was neither part of the current workflow, part of a rerun workflow, a retry of a previous job, nor from a different branch"
      echo "----------------------------------------------------------------------------------------------------"

      FOUND_BASE_COMPARE_COMMIT=true
    else
      echo "----------------------------------------------------------------------------------------------------"
      echo "job $JOB_NUM was part of the current workflow, part of a rerun workflow, a retry of a previous job, or from a different branch"
      echo "----------------------------------------------------------------------------------------------------"
      JOB_NUM=$(( $JOB_NUM - 1 ))
      continue
    fi
  fi
done

## CONCLUSION

# clean up
rm -f JOB_OUTPUT

BASE_COMPARE_COMMIT=$(extract_commit_from_job $VCS_TYPE $JOB_NUM)

# construct our compare URL, based on VCS type
if [[ $(echo $VCS_TYPE | grep github) ]]; then
  CIRCLE_COMPARE_URL="https://github.com/$CIRCLE_PROJECT_USERNAME/$CIRCLE_PROJECT_REPONAME/compare/${BASE_COMPARE_COMMIT:0:12}...${CIRCLE_SHA1:0:12}"
else
  CIRCLE_COMPARE_URL="https://bitbucket.org/$CIRCLE_PROJECT_USERNAME/$CIRCLE_PROJECT_REPONAME/branches/compare/${BASE_COMPARE_COMMIT:0:12}...${CIRCLE_SHA1:0:12}"
fi


echo "----------------------------------------------------------------------------------------------------"
echo "base compare commit hash is:" $BASE_COMPARE_COMMIT
echo ""
echo $BASE_COMPARE_COMMIT > BASE_COMPARE_COMMIT.txt
echo "this job's commit hash is:" $CIRCLE_SHA1
echo "----------------------------------------------------------------------------------------------------"
echo "recreated CIRCLE_COMPARE_URL:"
echo $CIRCLE_COMPARE_URL
echo "----------------------------------------------------------------------------------------------------"
echo "outputting CIRCLE_COMPARE_URL to a file in your working directory, called CIRCLE_COMPARE_URL.txt"
echo "(BASE_COMPARE_COMMIT has also been stored in your working directory as BASE_COMPARE_COMMIT.txt)"
echo $CIRCLE_COMPARE_URL > CIRCLE_COMPARE_URL.txt
echo "----------------------------------------------------------------------------------------------------"
echo "next: both CIRCLE_COMPARE_URL.txt and BASE_COMPARE_COMMIT.txt will be persisted to a workspace, in case they are needed in later jobs"
