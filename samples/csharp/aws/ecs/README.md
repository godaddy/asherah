# Encrypt/decrypt sample application on Amazon ECS

This sample application and tutorial demonstrates the use of Asherah SDK to perform application-layer encryption
operations in a web API built with ASP.NET Core and launched as a Fargate task on Amazon ECS.

> This example is based on the excellent ECS tutorial found [here](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-cli-tutorial-fargate.html)
in the [AWS Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html).

### Prerequisites

* [The AWS CLI (version 2)](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) and access to an AWS account
* [Amazon ECS CLI](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ECS_CLI_installation.html)
* [Docker](https://docs.docker.com/get-docker/)
* The Bash shell

>Optionally, if you wish to build and run the sample application locally you will need [.NET SDK 3.1 or later](https://dotnet.microsoft.com/download/dotnet/3.1) installed in your development environment.

In addition, the steps that follow also assume the `ecsTaskExecutionRole` already exists within your AWS account. If you
know this task execution role already exists, you're ready to [get started](#setup).
Otherwise, see
[this section](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-cli-tutorial-fargate.html#ECS_CLI_tutorial_fargate_iam_role)
of the ECS tutorial for more information.

## Setup

Clone this repository and navigate to the sample application's root directory.

```console
[user@machine ~]$ git clone git@github.com:godaddy/asherah.git
[user@machine ~]$ cd asherah/samples/csharp/aws/ecs
```

## Build container image and push to ECR
Create a repository for the container image

```console
[user@machine ecs]$ aws ecr create-repository \
    --repository-name asherah-samples/ecs-csharp --region us-west-2
{
    "repository": {
        "repositoryArn": "arn:aws:ecr:us-west-2:123456789012:repository/asherah-samples/ecs-csharp",
        "registryId": "123456789012",
        "repositoryName": "asherah-samples/ecs-csharp",
        "repositoryUri": "123456789012.dkr.ecr.us-west-2.amazonaws.com/asherah-samples/ecs-csharp",
        "createdAt": 1586467301.0,
        "imageTagMutability": "MUTABLE"
    }
}
```

Create a couple `env` variables for convenience
```console
[user@machine ecs]$ REPO_ROOT=123456789012.dkr.ecr.us-west-2.amazonaws.com
[user@machine ecs]$ export MYAPP_IMAGE="${REPO_ROOT}/asherah-samples/ecs-csharp"
```

Ensure your docker client is currently authenticated to your Amazon ECR registry
```console
[user@machine ecs]$ aws ecr get-login-password --region us-west-2 |
    docker login --username AWS --password-stdin $REPO_ROOT
Login Succeeded
```
> :exclamation: If you encounter an error running the above command, ensure
[version 2](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) of the AWS CLI is installed.

Build the image using `docker-compose`
```console
[user@machine ecs]$ docker-compose build
...
Successfully built a81000f6d4d2
Successfully tagged 123456789012.dkr.ecr.us-west-2.amazonaws.com/asherah-samples/ecs-csharp:latest
```

Push the image to the remote repository
```console
[user@machine ecs]$ docker push $MYAPP_IMAGE
...
```

## Configure the Amazon ECS CLI

Create a cluster configuration
```console
[user@machine ecs]$ ecs-cli configure \
    --cluster ecs-demo \
    --default-launch-type FARGATE \
    --config-name ecs-demo \
    --region us-west-2
```

Create a CLI profile using your access key and secret key
```console
[user@machine ecs]$ ecs-cli configure profile \
    --access-key ${AWS_ACCESS_KEY_ID} \
    --secret-key ${AWS_SECRET_ACCESS_KEY} \
    --profile-name my_account
```

## Create a cluster
Create a new cluster using `ecs-cli up`

```console
[user@machine ecs]$ ecs-cli up --cluster-config ecsdemo --ecs-profile my_account
INFO[0001] Created cluster                               cluster=ecsdemo region=us-west-2
INFO[0003] Waiting for your cluster resources to be created...
INFO[0004] Cloudformation stack status                   stackStatus=CREATE_IN_PROGRESS
INFO[0065] Cloudformation stack status                   stackStatus=CREATE_IN_PROGRESS
VPC created: vpc-0123456789abcdef0
Subnet created: subnet-0123456789abcdef0
Subnet created: subnet-0123456789abcdef1
Cluster creation succeeded.
```

Modify the provided `ecs-params.yml` file by replacing the placeholder subnet and security group IDs.

**NOTE**: The subnet IDs were provided by the previous step and the security group ID for your cluster's VPC can be
retrieved with the following

```console
[user@machine ecs]$ aws ec2 describe-security-groups \
    --filters Name=vpc-id,Values=vpc-0123456789abcdef0 \
    --region us-west-2 | jq -r '.SecurityGroups[0].GroupId'
sg-0123456789abcdef0
```

Your modified `ecs-params.yml` file should now look something this:

```yaml
version: 1
task_definition:
  task_execution_role: ecsTaskExecutionRole
  ecs_network_mode: awsvpc
  task_size:
    mem_limit: 0.5GB
    cpu_limit: 256
run_params:
  network_configuration:
    awsvpc_configuration:
      subnets:
        - "subnet-0123456789abcdef0"
        - "subnet-0123456789abcdef1"
      security_groups:
        - "sg-0123456789abcdef0"
      assign_public_ip: ENABLED
```

## Configure the security group
Add a security group rule to allow inbound access on port 8000 which is exposed to the host by the container
```console
[user@machine ecs] aws ec2 authorize-security-group-ingress \
    --group-id sg-0123456789abcdef0 \
    --protocol tcp \
    --port 8000 \
    --cidr 0.0.0.0/0 \
    --region us-west-2
```

## Deploy the task
Now you're ready to deploy the service. Use `ecs-cli compose service up` to deploy the task to your cluster
```console
[user@machine ecs]$ ecs-cli compose \
    --file docker-compose.yml \
    --file docker-compose.aws.yml \
    --project-name asherah-ecs-csharp \
    service up --create-log-groups --cluster-config ecsdemo --ecs-profile my_account
...
```

Once the task has been launched `ecs-cli compose service ps` can be used to view the running containers
```console
[user@machine ecs]$ ecs-cli compose \
    --project-name asherah-ecs-csharp \
    service ps --cluster-config ecsdemo --ecs-profile my_account
Name                                            State    Ports                         TaskDefinition        Health
ecsdemo/d6fe886eb785475ba3c9939b823d8ea8/myapp  RUNNING  34.212.31.202:8000->8000/tcp  asherah-ecs-csharp:1  UNKNOWN
```

Use `ecs-cli logs` to view the container logs for the running task
```console
[user@machine ecs]$ ecs-cli logs \
    --task-id d6fe886eb785475ba3c9939b823d8ea8 \
    --cluster-config ecsdemo \
    --ecs-profile my_account \
    --follow
```

> Note that the `task-id` value can be found in the `ecs-cli compose service ps` output above and the
`--follow` option instructs the ECS CLI to continuously poll for logs.

## Test
You can test the deployment by invoking the API from the terminal.

First open a new terminal and use `curl` to retrieve the list of customers (which will be empty at this point)

```console
[user@machine ~]$ curl http://34.212.31.202:8000/api/customers
[]
```

> :exclamation: If you experience issues connecting to the service, ensure the security group has been configured to
allow inbound access as described [above](#configure-the-security-group).

Add a new customer via a POST to the same API endpoint. First, create a file named `customer.json` with the following
content:

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "address": "321 Elm St"
}
```

...and now post this customer data to the endpoint

```console
[user@machine ~]$ curl \
    -H 'Content-type: application/json' \
    -d @customer.json \
    http://34.212.31.202:8000/api/customers
{"id":"181141cd-090a-488b-9e5a-2bd1948046cd","created":"2021-02-26T00:25:05.7775978Z","secretInfo":"eyJLZXkiOnsiQ3JlYXRlZCI6MTYxNDI5OTEwNSwiS2V5IjoiREtSLytqdGVxaGx6OXVUQmIwN1FReVFCS1I4aWlUMzhaNkZ2QUxMM3I3aDZMKzlTck9jTzdCKzRHV2E3SXE1OUJzMzBnTnNxNFFMcVR4S1ciLCJQYXJlbnRLZXlNZXRhIjp7IktleUlkIjoiX0lLXzE4MTE0MWNkLTA5MGEtNDg4Yi05ZTVhLTJiZDE5NDgwNDZjZF9hcGlfbXlhcHAiLCJDcmVhdGVkIjoxNjE0Mjk5MTAwfX0sIkRhdGEiOiJ2RzR3cTdsNDNUdTRCd0c2TE9vS3ZKUlVWM2c5RWpQME5OYXo3NUswZlRjRUlyYUlWS3NDWjc4d0lrRXQzSEdMcXh6dEIwNjBqazZwTXNWUEhvVnlvRXRCWGU5b0NKcUc5TVE1WFFySzYxWTdnTm9TY2dPT1ZnPT0ifQ=="}
```

The newly created customer resource includes an `id` which can be used for later retrieval

```console
[user@machine ~]$ curl http://34.212.31.202:8000/api/customers/181141cd-090a-488b-9e5a-2bd1948046cd
...
```

...and as you'd expect, the new customer is now included in the customers list
```console
[user@machine ~]$ curl http://34.212.31.202:8000/api/customers
[{"id":"181141cd-090a-488b-9e5a-2bd1948046cd","created":"2021-02-26T00:25:05.7775978Z","secretInfo":"eyJLZXkiOnsiQ3JlYXRlZCI6MTYxNDI5OTEwNSwiS2V5IjoiREtSLytqdGVxaGx6OXVUQmIwN1FReVFCS1I4aWlUMzhaNkZ2QUxMM3I3aDZMKzlTck9jTzdCKzRHV2E3SXE1OUJzMzBnTnNxNFFMcVR4S1ciLCJQYXJlbnRLZXlNZXRhIjp7IktleUlkIjoiX0lLXzE4MTE0MWNkLTA5MGEtNDg4Yi05ZTVhLTJiZDE5NDgwNDZjZF9hcGlfbXlhcHAiLCJDcmVhdGVkIjoxNjE0Mjk5MTAwfX0sIkRhdGEiOiJ2RzR3cTdsNDNUdTRCd0c2TE9vS3ZKUlVWM2c5RWpQME5OYXo3NUswZlRjRUlyYUlWS3NDWjc4d0lrRXQzSEdMcXh6dEIwNjBqazZwTXNWUEhvVnlvRXRCWGU5b0NKcUc5TVE1WFFySzYxWTdnTm9TY2dPT1ZnPT0ifQ=="}]
```

### Taking a closer look...
At this point you may be wondering what happened to the customer information present in the original post data. The
`customer.json` content included `firstName`, `lastName`, and `address`, none of which appear to be included in the
above responses from the API. Rest assured the data hasn't been lost, on the contrary, one could say that it's been
hidden in plain sight (hint: `secretInfo`).

The sample application uses application-layer encryption to safeguard this sensitive customer information. Upon creating
a new customer, the API extracts these fields from the original request, encrypts the sensitive data using the Asherah
SDK, and stores the encrypted result in the `secretInfo` field. Check out the [code](./myapp) for the full
implementation.

For demonstration purposes, the sample application includes a method for decrypting this data. The full customer
resource can be retrieved via a GET to the `/api/customers/{id}/full` endpoint

```console
[user@machine ~]$ curl http://34.212.31.202:8000/api/customers/181141cd-090a-488b-9e5a-2bd1948046cd/full
{
  "id": "181141cd-090a-488b-9e5a-2bd1948046cd",
  "created": "2021-02-26T00:25:05.7775978Z",
  "firstName": "Jane",
  "lastName": "Doe",
  "address": "321 Elm St"
}
```

## Cleaning up
First, tear down the service to shut down the running containers

```console
[user@machine ecs]$ ecs-cli compose \
    --project-name asherah-ecs-csharp \
    service down --cluster-config ecsdemo --ecs-profile my_account
```

Next, clean up the resources that you created earlier using `ecs-cli up` with the `ecs-cli down` command

```console
[user@machine ecs]$ ecs-cli down --force --cluster-config ecsdemo --ecs-profile my_account
```
