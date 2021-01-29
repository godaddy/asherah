#!/bin/bash

set -eo pipefail

ARTIFACT_BUCKET=$(cat out/bucket-name.txt)

cd function
GOOS=linux go build main.go
cd ../

aws cloudformation package --template-file template.yml --s3-bucket $ARTIFACT_BUCKET --output-template-file out/out.yml
aws cloudformation deploy --template-file out/out.yml --stack-name sample-lambda-go --capabilities CAPABILITY_NAMED_IAM
