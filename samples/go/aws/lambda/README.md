# Encrypt/decrypt sample application for AWS Lambda

The encrypt/decrypt sample application demonstrates the use of Asherah SDK to perform application-level encryption
operations in an AWS Lambda function.

> This example is based on the [Blank function sample application](https://github.com/awsdocs/aws-lambda-developer-guide/tree/main/sample-apps/blank-go) found in the
[AWS Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html).

### Prerequisites

* [The AWS CLI (version 2)](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) and access to an AWS account
* [Go v1.15+](https://golang.org/doc/install)
* The Bash shell
* [jq](https://stedolan.github.io/jq/) - a lightweight and flexible command-line JSON processor

In addition, the steps that follow assume the `lambda-exec` role already exists within your AWS account. If you know this function
execution role already exists, you're ready to [get started](#setup). Otherwise, see
[this section](https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-awscli.html#with-userapp-walkthrough-custom-events-create-iam-role)
of the AWS Lambda tutorial for more information.

Likewise, the sample application is configured to use AWS KMS for master key operations and DynamoDB as a metastore. You
will need to ensure the above role has sufficient access to these services and associated resources or function execution
will result in error. For more information on these topics see [Key Management Service](/docs/KeyManagementService.md)
and [Metastore](/docs/Metastore.md) in the Asherah documentation.

## Setup

Clone this repository and navigate to the sample application's root directory.

```console
$ git clone git@github.com:godaddy/asherah.git
$ cd asherah/samples/go/aws/lambda
```

Add the following to your
[configuration file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html) (`~/.aws/config`) to
enable loading of raw JSON events with the AWS CLI v2:

```
cli_binary_format=raw-in-base64-out
```

Create the `lambda-exec` role, if needed.

```console
$ aws iam create-role --role-name lambda-exec --assume-role-policy-document file://policy.json
{
    "Role": {
        "Path": "/",
        "RoleName": "lambda-exec",
        "RoleId": "AROAWOYE3S3E7IEJN54CD",
        "Arn": "arn:aws:iam::123456789012:role/lambda-exec",
        "CreateDate": "2021-01-10T20:05:52+00:00",
        "AssumeRolePolicyDocument": {
            "Version": "2012-10-17",
            "Statement": [
                {
                    "Effect": "Allow",
                    "Principal": {
                        "Service": "lambda.amazonaws.com"
                    },
                    "Action": "sts:AssumeRole"
                }
            ]
        }
    }
}
```

The file `policy.json` is a JSON document in the current directory that defines the trust policy for the role. In this
case the policy allows Lambda to use the role's permissions via the `AssumeRole` action.

Example `policy.json`

```json
{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Service": "lambda.amazonaws.com"
        },
        "Action": "sts:AssumeRole"
      }
    ]
  }
```

Now you can add permissions to the role, starting with the `AWSLambdaBasicExecutionRole` managed policy.

```console
$ aws iam attach-role-policy \
    --role-name lambda-exec \
    --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
```

The above command will need to be repeated for any additional permissions. The sample application will also need
permissions granted by the following policies:
* `arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess`: a managed policy granting write only permissions to AWS X-Ray
* `arn:aws:iam::123456789012:policy/asherah-kms-access`: a customer managed policy granting access to a customer master
key, see [KMS Permissions](/docs/KeyManagementService.md#creating-an-aws-kms-key).
* `arn:aws:iam::123456789012:policy/asherah-dynamodb-access`: a customer managed policy granting access to a DynamoDB
table, see [Metastore: DynamoDB](/docs/Metastore.md#dynamodb)

Modify the provided [template.yml](template.yml) file by replacing the placeholder KMS Key and Role ARNs. Your updated
`template.yml` file should now resemble the following:

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: 'AWS::Serverless-2016-10-31'
Description: An AWS Lambda application that demonstrates Asherah encrypt/decrypt operations.
Resources:
  function:
    Type: AWS::Serverless::Function
    Properties:
      Environment:
        Variables:
          ASHERAH_KMS_KEY_ARN: arn:aws:kms:us-west-2:123456789012:key/1234abcd-56ef-78ab-90cd-1a2b3c4d5e6f
          ASHERAH_METASTORE_TABLE_NAME: EncryptionKey
      Handler: main
      Runtime: go1.x
      CodeUri: function/.
      Description: Performs encrypt/decrypt operations via the Asherah SDK
      Timeout: 5
      # Function's execution role
      Role: arn:aws:iam::123456789012:role/lambda-exec
      Tracing: Active
```

Create a new bucket for deployment artifacts, run `1-create-bucket.sh`.

```console
$ ./1-create-bucket.sh
make_bucket: lambda-artifacts-dc816d4fef315985
```

## Deploy

To deploy the application, run `2-deploy.sh`.

```console
$ ./2-deploy.sh
Successfully packaged artifacts and wrote output template to file out/out.yml.
Waiting for changeset to be created..
Waiting for stack create/update to complete
Successfully created/updated stack - sample-lambda-go
```

This script uses AWS CloudFormation to deploy the Lambda function. If the AWS CloudFormation stack that contains the
resources already exists, the script updates it with any changes to the template or function code.

## Test

To invoke the function, run `3-invoke.sh`.

```console
$ ./3-invoke.sh

Encrypt
=======
invoking function with encrypt payload:
{"Name":"encrypt-partition-1","Partition":"partition-1","Payload":"bXlzdXBlcnNlY3JldHRleHQ="}
-------
Response received (modified):
{"Results":{"Key":{"Created":1610062226,"Key":"Uz4jvKT4EiRMfee7pmgW/r1etnLvu/vChsGGsQ3dJHvm8OXK9eeODxP+mPJoM/0i/yytwA48wP1jsM03","ParentKeyMeta":{"KeyId":"_IK_partition-1_asherah-samples_lambda-sample-app","Created":1607473080}},"Data":"gapc/YbQrhAgCAmovpI/Q64ICs3kQSv2QVNkcaCtsIFy3fAIV1C4+11ObOHO"},"Metrics":{"InvocationCount":1,"SecretsAllocated":3,"SecretsInUse":2}}

Decrypt
=======
invoking function with decrypt payload:
{"Name":"decrypt-partition-1","Partition":"partition-1","DRR":{"Key":{"Created":1610062226,"Key":"Uz4jvKT4EiRMfee7pmgW/r1etnLvu/vChsGGsQ3dJHvm8OXK9eeODxP+mPJoM/0i/yytwA48wP1jsM03","ParentKeyMeta":{"KeyId":"_IK_partition-1_asherah-samples_lambda-sample-app","Created":1607473080}},"Data":"gapc/YbQrhAgCAmovpI/Q64ICs3kQSv2QVNkcaCtsIFy3fAIV1C4+11ObOHO"}}
-------
Response received (modified):
{"Results":"mysupersecrettext","Metrics":{"InvocationCount":2,"SecretsAllocated":3,"SecretsInUse":2}}
```

Now, assuming all went as planned, your console output should resemble the above. Cool, but what just happened?

### Taking a closer look...

The script invokes the function two times, printing the results as it goes, then exits.

As seen above, the payload used for the first is a string that contains an event in JSON format.

```json
{"Name":"encrypt-partition-1","Partition":"partition-1","Payload":"bXlzdXBlcnNlY3JldHRleHQ="}
```

This event is handled by the sample application as an _encryption request_, prompting the app to use the Asherah SDK to
encrypt the provided payload and return the encryption result.

Next, the script invokes the function with the following:

```json
{"Name":"decrypt-partition-1","Partition":"partition-1","DRR":{...}}
```

This time the event handled as a _decryption request_, prompting the app to use the Asherah SDK to decrypt the cyphertext
contained in this payload's DRR. Note that the DRR embedded in this payload is the same DRR provided by the encryption
result above.

## The code

The Go module containing our Lambda function can be found in the [./function](./function) directory. And all code
for the sample app is in [main.go](./function/main.go).

Let's take a look at an abbreviated version of this code.

```go
package main

import (
  // ...

  "github.com/aws/aws-lambda-go/lambda"
  "github.com/godaddy/asherah/go/appencryption"
)

var (
  // ...
  factory *appencryption.SessionFactory
)

type MyEvent struct {
  Name      string
  Partition string
  Payload   []byte                       `json:",omitempty"`
  DRR       *appencryption.DataRowRecord `json:",omitempty"`
}

type MyResponse struct {
  PlainText string                       `json:",omitempty"`
  DRR       *appencryption.DataRowRecord `json:",omitempty"`
  // ...
}

type recoveredError struct{ error }

func (e recoveredError) isRetryable() bool {
  // ...
}

func HandleRequest(ctx context.Context, event MyEvent) (*MyResponse, error) {
  initFactory()

  resp, err := tryHandle(ctx, event)
  if err != nil {
    if r, ok := err.(recoveredError); ok && r.isRetryable() {
      if err := resetFactory(); err != nil {
        return nil, err
      }

      return tryHandle(ctx, event)
    }
  }

  return resp, err
}

func initFactory() error {
  if factory != nil {
    return nil
  }

  factory = appencryption.NewSessionFactory(
    // ...
    Policy: appencryption.NewCryptoPolicy(
      appencryption.WithSessionCache(),
      appencryption.WithSessionCacheMaxSize(10),
    ),
  )

  return nil
}

func resetFactory() error {
  factory.Close()
  factory = nil

  return initFactory()
}

func tryHandle(ctx context.Context, event MyEvent) (resp *MyResponse, err error) {
  defer func() {
    if r := recover(); r != nil {
      if e, ok := r.(error); ok {
        err = recoveredError{e}
      } else {
        panic(r)
      }
    }
  }()

  switch {
  case len(event.Payload) > 0:
    return handleEncrypt(ctx, event)
  case event.DRR != nil:
    return handleDecrypt(ctx, event)
  default:
    return nil, errors.New("event must contain a Payload (for encryption) or DRR (for decryption)")
  }
}

func handleEncrypt(ctx context.Context, event MyEvent) (*MyResponse, error) {
  session, _ := factory.GetSession(event.Partition)
  defer session.Close()

  encData, _ := session.Encrypt(ctx, event.Payload)

  return &MyResponse{
    DRR:  encData,
  }, nil
}

func handleDecrypt(ctx context.Context, event MyEvent) (*MyResponse, error) {
  session, _ := factory.GetSession(event.Partition)
  defer session.Close()

  plaintext, _ := session.Decrypt(ctx, *event.DRR)

  return &MyResponse{
    PlainText: string(plaintext),
  }, nil
}

func main() {
  lambda.Start(HandleRequest)
}
```

The simplified code above omits most error handling, logging, metrics, and even import code (`go build` beware!), that
said, there's still a lot going on...

### Items of note

* **package main**: In Go, the package containing `func main()` must always be named `main`.
* **import**: All of the packages required by the Lambda function are imported here. In this case, we'll highlight two:
  * **github.com/aws/aws-lambda-go/lambda**: implements the AWS Lambda programming model for Go.
  * **github.com/godaddy/asherah/go/appencryption**: provides the main Asherah SDK implementations for Go.
* **`func HandleRequest(ctx context.Context, event MyEvent) (*MyResponse, error)`**: This is the handler for our
  Lambda function and will be executed each time the function is invoked. When building a function handler in Go, you
  several [valid handler signatures](https://docs.aws.amazon.com/lambda/latest/dg/golang-handler.html#golang-handler-signatures)
  to choose from. In our case, we use the following parameters:
  * `ctx context.Context`: provides cancellation signals, deadlines, and Lambda-specific runtime information via the
    [github.com/aws/aws-lambda-go/lambdacontext](https://pkg.go.dev/github.com/aws/aws-lambda-go/lambdacontext) package.
  * `event MyEvent`: contains the structured event data that was passed to the handler.
  * `*MyResponse, error`: the structured response data returned by the handler and standard `error` information.
* **`func initFactory() error`**: initializes global state by setting `var factory *appencryption.SessionFactory`. This
  approach is considered a [best practice](https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html) as it takes
  advantage of the Lambda execution environment by allowing subsequest invocations of a function instance to reuse this
  global `factory` resource. This saves cost by reducing function run time.
  >Note that synchronization isn't needed here
  because a single instance will **never** handle multiple events simultaneously.
* **`func tryHandle(ctx context.Context, event MyEvent) (resp *MyResponse, err error)`**: called by our function handler
  to perform the requested encrypt or decrypt operation. In the event of a panic, it attempts recovery and returns a
  `recoveredError` to the handler if successful.
* **`type recoveredError struct{ error }`**: used to indicate a panic has occurred while attempting to handle the event,
  in which case the handler _may_ attempt to retry handling the event (more on this below).
* **`func main()`**: registers the handler via `lambda.Start(HandleRequest)` and ultimately runs our Lambda function
  code. This is required.

As noted in the section above, the sample application reuses initialized resources to reduce run time which in turn
reduces cost. By reusing the `appencryption.SessionFactory` specifically, we can take advantage of Asherah's session
and key caching functionality to minimize calls to supporting services such as AWS KMS and DynamoDB, bringing additional
cost savings.

When developing with Asherah on AWS Lambda there's one resource limitation in particular worth special consideration. At
the time of this writing, lockable memory (memlock) in the Lambda execution environment is limited to 64KiB. This is
important because, as noted in [System Requirements](/docs/SystemRequirements.md#memory-usage), Asherah requires 4KiB of
lockable memory per key. This means that, at most, the total number of keys that can be loaded into memory at any given
time is 16, a low number for sure but one that's manageable with the right precautions.

The sample application accounts for this in a few ways:
* encryption operations are limited to a single short-lived session, requiring only one system key and one intermediate
  key per function invocation.
* `appencryption.WithSessionCacheMaxSize(10)` limits the maximum size of the session cache to 10.
  >If you determine session caching isn't a good fit for your use case, it can be disabled by simply omitting this along with
  the preceeding `appencryption.WithSessionCache()` option. Likewise, the caching of both System and Intermediate keys
  can be disabled entirely via the `appencryption.WithNoCache()` option. _Note that disabling caching altogether will
  result in increased latency (and cost) as keys will need to be retrieved from the metastore on every encrypt/decrypt
  operation._
* panicking goroutines are recovered and errors are inspected using `recoveredError.isRetryable()`. In the event the
  the underlying error was triggered by exceeding the memlock limit, this function returns `true`, prompting the handler
  to reinitialize the `SessionFactory` before retrying the original operation.
