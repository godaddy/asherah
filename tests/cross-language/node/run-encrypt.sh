#!/bin/bash

set -eu

echo "Create local copy of encrypt.feature for Cucumber.js"
cp ../features/encrypt.feature ./features/

echo "Running encrypt.feature"
./node_modules/.bin/cucumber-js ./features/encrypt.feature

echo "Clean up local copy of encrypt.feature"
rm ./features/encrypt.feature
