#!/bin/bash

FUNCTION_NAME=$(aws cloudformation describe-stack-resource \
    --stack-name sample-lambda-go \
    --logical-resource-id "function" \
    --query 'StackResourceDetail.PhysicalResourceId' \
    --output text)

encrypt() {
    local partition=${1:-"partition-1"}
    local raw_payload=${2:-"mysupersecrettext"}
    local encoded="$(echo -n "${raw_payload}" | base64)"
    local payload="{\"Name\": \"encrypt-${partition}\", \"Partition\": \"${partition}\", \"Payload\": \"${encoded}\"}"
    local dest="out/out-encrypt.json"

    echo
    echo "Encrypt"
    echo "======="
    echo "invoking function with encrypt payload:"
    echo $payload | jq -c .

    aws lambda invoke --function-name $FUNCTION_NAME --payload "${payload}" $dest 1>/dev/null && print_results $dest
}

print_results() {
    local outfile=$1
    local errorMessage="$(jq -r '.errorMessage // empty' $outfile)"
    if [[ ! -z "$errorMessage" ]]; then
        echo $errorMessage
        return 1
    fi

    echo "-------"
    echo "Response received (modified):"
    # Depending on the request type, i.e., encrypt or decrypt, the response JSON will contain either a DRR or PlainText
    # attribute. The command below constructs a new JSON object consisting of whichever is present along with a few of
    # the metrics collected by the sample application and included in the response.
    jq -c '. | {Results: (.DRR // .PlainText), Metrics: {InvocationCount: .Metrics["asherah.samples.lambda-go.invocations"].count, SecretsAllocated: .Metrics["secret.allocated"].count, SecretsInUse: .Metrics["secret.inuse"].count}}' $outfile

    # Replace the above the following (commented) command to print the entire function response JSON
    # jq . $outfile
}

decrypt() {
    local partition=${1:-"partition-1"}
    local payload="{\"Name\": \"decrypt-${partition}\", \"Partition\": \"${partition}\", \"DRR\": $(jq -c .DRR out/out-encrypt.json)}"
    local dest="out/out-decrypt.json"

    echo
    echo "Decrypt"
    echo "======="
    echo "invoking function with decrypt payload:"
    echo "$payload" | jq -c .

    # Note that the payload contains the DRR contained in the previous decrypt response
    aws lambda invoke --function-name $FUNCTION_NAME --payload "${payload}" $dest 1>/dev/null && print_results $dest
}

encrypt "$@" && decrypt "$@"
