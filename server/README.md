# Asherah Server
Asherah Server is intended to be used by those who wish to utilize application-layer encryption but are unable to take
advantage of the SDK directly, e.g., Asherah lacks an implementation in their preferred programming language.

Table of Contents
=================

  * [Overview](#overview)
  * [Samples](#samples)
    * [Docker Compose](#docker-compose)
    * [Kubernetes](#kubernetes-kind)
    * [Amazon ECS](#amazon-ecs)
  * [Server Development](#server-development)

## Overview
Asherah Server is as a light-weight service layer built atop the Asherah SDK with encrypt/decrypt functionality exposed
via a [gRPC](https://grpc.io) service. It uses a Unix domain socket for local inter-process communication and is
designed to be deployed along side your application.

To integrate with the service you will need to generate client interfaces from the
[.proto](./protos/appencryption.proto) service definition. Detailed instructions for generating client code in a number
of languages can be found in the [gRPC Quick Starts](https://grpc.io/docs/quickstart/).

## Samples
Full code for the client implementations used in the following samples, as well as the multi-container application
configurations, can be found in the [samples](./samples) directory.

### Docker Compose
Use Docker Compose to launch a multi-container application comprised of a simple Python client and an Asherah Server
sidecar.

#### Prerequisites
Ensure Docker Compose is installed on your local system. For installation instructions, see
[Install Docker Compose](https://docs.docker.com/compose/install/).

#### Build and run the sample application
From the [samples](./samples) directory run `docker-compose up` which will launch the application defined in
[docker-compose.yaml](./samples/docker-compose.yaml).

```console
[user@machine samples]$ docker-compose up
Creating network "samples_default" with the default driver
Creating samples_sidecar_1 ... done
Creating samples_myapp_1   ... done
Attaching to samples_sidecar_1, samples_myapp_1
sidecar_1  | 2020/04/02 19:06:36 starting server
myapp_1    | INFO:root:starting test
myapp_1    | INFO:root:starting session for partitionid-1
sidecar_1  | 2020/04/02 19:06:37 handling get-session for partitionid-1
```

At this point the sample client begins sending encrypt and decrypt messages to the server sidecar in a loop and you will
see stream of log messages from both the client (myapp_1) and server (sidecar_1). Enter `CTRL+c` to shutdown the
application.

**NOTE**: Docker Compose will need to build the images for both containers the first time `docker-compose up` is run on
your machine. This process typically takes between 5 to 10 minutes, but build times can vary considerably from one
machine to another.

### Kubernetes (kind)
Launch the the same multi-container application we used above but this time we'll use [kind](https://kind.sigs.k8s.io/)
to spin up a local Kubernetes cluster and `kubectl` to launch the deployment.

#### Prerequisites
Ensure both `kind` and `kubectl` are installed locally.

* [Installing kind](https://kind.sigs.k8s.io/docs/user/quick-start/#installation)
* [Installing kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/)

#### Build and launch the sample deployment
From the [samples](./samples) directory run `docker build` to build the docker image for the provided Python client:

```console
[user@machine samples]$ docker build -t sample-client clients/python
Sending build context to Docker daemon  3.375MB
Step 1/15 : FROM python:3.7-alpine as base
... snipped
Step 14/15 : ENTRYPOINT ["python", "appencryption_client.py"]
 ---> Running in a0958391c52e
Removing intermediate container a0958391c52e
 ---> 838466cf27e6
Step 15/15 : CMD ["--help"]
 ---> Running in e94395d01e20
Removing intermediate container e94395d01e20
 ---> c0a177a10252
Successfully built c0a177a10252
Successfully tagged sample-client:latest
```

we'll also need the server image:

```console
[user@machine samples]$ docker build -t asherah-server ../go
Sending build context to Docker daemon  87.04kB
Step 1/19 : ARG GOVERSION=1.13
... snipped
Step 18/19 : ENTRYPOINT ["/asherah-server"]
 ---> Using cache
 ---> 2fdfbe426e0e
Step 19/19 : CMD ["--help"]
 ---> Using cache
 ---> a3bc3c0483a8
Successfully built a3bc3c0483a8
Successfully tagged asherah-server:latest
```

Now that we have both of the images we can run `kind` to create our local Kubernetes cluster and then load the new
images into the cluster:

```console
[user@machine samples]$ kind create cluster
...
[user@machine samples]$ kind load docker-image sample-client
...
[user@machine samples]$ kind load docker-image asherah-server
...
```

And now we're ready to use `kubectl` launch the deployment defined in [deployment.yaml](./samples/deployment.yaml):

```console
[user@machine samples]$ kubectl --context kind-kind apply -f deployment.yaml
deployment.apps/myapp created
```

We can view our pod and the application logs to verify everything is in working order:

```console
[user@machine samples]$ kubectl --context kind-kind get po
NAME                     READY   STATUS    RESTARTS   AGE
myapp-77bd786698-ls42l   2/2     Running   0          70s
[user@machine samples]$ kubectl --context kind-kind logs -c myapp myapp-77bd786698-ls42l | head -n2
INFO:root:starting test
INFO:root:starting session for partitionid-1
```

And when finished, `kind delete cluster` can be used to delete the cluster.

### Amazon ECS
Launch the sample application as a Fargate task on Amazon ECS.

> **NOTE**: This example is based on the excellent ECS tutorial found
[here](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-cli-tutorial-fargate.html) in the
[AWS Developer Guide](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/Welcome.html).

#### Prerequisites
Ensure you have access to an AWS account and both Amazon ECS CLI and AWS CLI (version 2) are installed.

* [Installing Amazon ECS CLI](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ECS_CLI_installation.html)
* [Installing AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html)

In addition, the steps that follow also assume the `ecsTaskExecutionRole` already exists within your AWS account. If you
know this task execution role already exists, you're ready to [get started](#create-an-ecr-repository-and-push-images).
Otherwise, see the
[this section](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-cli-tutorial-fargate.html#ECS_CLI_tutorial_fargate_iam_role)
of the ECS tutorial for more information.


#### Create an ECR repository and push images
Create a repository for each image

```console
[user@machine samples]$ aws ecr create-repository \
    --repository-name asherah-samples/client-python --region us-west-2
{
    "repository": {
        "repositoryArn": "arn:aws:ecr:us-west-2:123456789012:repository/asherah-samples/client-python",
        "registryId": "123456789012",
        "repositoryName": "asherah-samples/client-python",
        "repositoryUri": "123456789012.dkr.ecr.us-west-2.amazonaws.com/asherah-samples/client-python",
        "createdAt": 1586467301.0,
        "imageTagMutability": "MUTABLE"
    }
}
[user@machine samples]$ aws ecr create-repository \
    --repository-name asherah-samples/server-go --region us-west-2
{
    "repository": {
        "repositoryArn": "arn:aws:ecr:us-west-2:123456789012:repository/asherah-samples/server-go",
        "registryId": "123456789012",
        "repositoryName": "asherah-samples/server-go",
        "repositoryUri": "123456789012.dkr.ecr.us-west-2.amazonaws.com/asherah-samples/server-go",
        "createdAt": 1586467577.0,
        "imageTagMutability": "MUTABLE"
    }
}
```

Export `env` variables containing the new names of the new repositories
```console
[user@machine samples]$ REPO_ROOT=123456789012.dkr.ecr.us-west-2.amazonaws.com
[user@machine samples]$ export ASHERAH_SAMPLE_CLIENT_IMAGE="${REPO_ROOT}/asherah-samples/client-python"
[user@machine samples]$ export ASHERAH_SAMPLE_SERVER_IMAGE="${REPO_ROOT}/asherah-samples/server-go"
```

Ensure your docker client is currently authenticated to your Amazon ECR registry
```console
[user@machine samples]$ aws ecr get-login-password --region us-west-2 |
    docker login --username AWS --password-stdin $REPO_ROOT
Login Succeeded
```
> :exclamation: If you encounter an error running the above command, ensure
[version 2](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) of the AWS CLI is installed.

Build and push the images to ECR
```console
[user@machine samples]$ docker build -t $ASHERAH_SAMPLE_CLIENT_IMAGE clients/python
...
[user@machine samples]$ docker build -t $ASHERAH_SAMPLE_SERVER_IMAGE ../go
...
[user@machine samples]$ docker push $ASHERAH_SAMPLE_CLIENT_IMAGE
...
[user@machine samples]$ docker push $ASHERAH_SAMPLE_SERVER_IMAGE
...
```

#### Create a cluster
Create a new cluster using `ecs-cli up`

```console
[user@machine samples]$ ecs-cli up --cluster-config ecsdemo --ecs-profile my_account
INFO[0001] Created cluster                               cluster=ecsdemo region=us-west-2
INFO[0003] Waiting for your cluster resources to be created...
INFO[0004] Cloudformation stack status                   stackStatus=CREATE_IN_PROGRESS
INFO[0065] Cloudformation stack status                   stackStatus=CREATE_IN_PROGRESS
VPC created: vpc-0123456789abcdef0
Subnet created: subnet-0123456789abcdef0
Subnet created: subnet-0123456789abcdef1
Cluster creation succeeded.
```

Modify the provided `ecs-params.yaml` file by replacing the placeholder subnet and security group IDs.

**NOTE**: The subnet IDs were provided by the previous step and the security group ID for your cluster's VPC can be
retrieved with the following

```console
[user@machine samples]$ aws ec2 describe-security-groups \
    --filters Name=vpc-id,Values=vpc-0123456789abcdef0 \
    --region us-west-2 | jq -r '.SecurityGroups[0].GroupId'
sg-0123456789abcdef0
```

Your modified `ecs-params.yaml` file should now look something this:

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

#### Deploy the task
Now you're ready to deploy the service. Use `ecs-cli compose service up` to deploy the task to your cluster.
```console
[user@machine samples]$ ecs-cli compose \
  --file docker-compose-ecs.yaml \
  --ecs-params ecs-params.yaml \
  --project-name asherah-sample-app \
  service up --create-log-groups --cluster-config ecsdemo --ecs-profile my_account
...
```

Once the task has been launched `ecs-cli compose service ps` can be used to view the running containers
```console
[user@machine samples]$ ecs-cli compose \
    --file docker-compose-ecs.yaml \
    --ecs-params ecs-params.yaml \
    --project-name asherah-sample-app \
    service ps --cluster-config ecsdemo --ecs-profile my_account
Name                                          State    Ports  TaskDefinition        Health
1ee1565c-82e8-4c1a-899d-f104ee009b37/sidecar  RUNNING         asherah-sample-app:1  UNKNOWN
1ee1565c-82e8-4c1a-899d-f104ee009b37/myapp    RUNNING         asherah-sample-app:1  UNKNOWN
```

And `ecs-cli logs` can be used to view the container logs
```console
[user@machine samples]$ ecs-cli logs \
    --task-id 1ee1565c-82e8-4c1a-899d-f104ee009b37 \
    --container-name myapp \
    --cluster-config ecsdemo \
    --ecs-profile my_account
INFO:root:received DRR
INFO:root:decrypting DRR
...
```

#### Cleaning up
That's it! When you're ready to wrap things up, be sure to clean up the resources by deleting the service and cluster
```console
[user@machine samples]$ ecs-cli compose \
    --file docker-compose-ecs.yaml \
    --ecs-params ecs-params.yaml \
    --project-name asherah-sample-app \
    service down --cluster-config ecsdemo --ecs-profile my_account
...
[user@machine samples]$ ecs-cli down --force --cluster-config ecsdemo --ecs-profile my_account
```

## Server Development

### Code generation
First you'll need to ensure the protobuf compiler and relevent gRPC packages have been installed for
language you're working with. See language sections below for more.

### Go
Use the following to regenerate the gRPC service code for the Go server implementation.

#### Prerequisites
Ensure you have a working Go installation and gRPC is installed.

* [Installing Go](https://golang.org/doc/install)
* [gRPC Quick Starts - Go](https://grpc.io/docs/quickstart/go/)

Navigate to the `server` directory and run the following:
```console
[user@machine server]$ protoc --go_out=plugins=grpc:./go protos/appencryption.proto
```

### Java
Coming soon.
