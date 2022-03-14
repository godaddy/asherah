#!/bin/bash

set -eu

echo "Create local copy of decrypt.feature for Cucumber.js"
cp ../features/decrypt.feature ./features/

echo "Running decrypt.feature"
./node_modules/.bin/cucumber-js ./features/decrypt.feature

echo "Clean up local copy of decrypt.feature"
rm ./features/decrypt.feature
