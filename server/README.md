# Asherah Server
Asherah Server is intended to be used by those who wish to utilize application-layer encryption but are unable to take advantage of the SDK directly, e.g., Asherah lacks an implementation in their preferred programming language.

Table of Contents
=================

  * [Overview](#overview)
  * [Examples](#examples)
    * [Docker Compose](#docker-compose)
    * [Kubernetes](#kubernetes-kind)
  * [Server Development](#server-development)

## Overview
Asherah Server is as a light-weight service layer built atop the Asherah SDK with encrypt/decrypt functionality exposed via a [gRPC](https://grpc.io) service. It uses a Unix domain socket for local inter-process communication and is designed to be deployed along side your application.

To integrate with the service you will need to generate client interfaces from the [.proto](./protos/appencryption.proto) service definition. Detailed instructions for generating client code in a number of languages can be found in the [gRPC Quick Starts](https://grpc.io/docs/quickstart/).

## Examples
Full code for the client implementations as well as the multi-container application configurations used in the following examples can be found in the [examples](./examples) directory.

### Docker Compose
In this example we'll use Docker Compose to launch a mutliple container application comprised of a simple Python client and an Asherah Server sidecar.

#### Prerequisites
Ensure Docker Compose is installed on your local system. For installation instructions, see [Install Docker Compose](https://docs.docker.com/compose/install/).

#### Build and run the example
From the [examples](./examples) directory run `docker-compose up` which will launch the application defined in [docker-compose.yaml](./examples/docker-compose.yaml).

```console
[user@machine examples]$ docker-compose up
Creating network "examples_default" with the default driver
Creating examples_sidecar_1 ... done
Creating examples_myapp_1   ... done
Attaching to examples_sidecar_1, examples_myapp_1
sidecar_1  | 2020/04/02 19:06:36 starting server
myapp_1    | INFO:root:starting test
myapp_1    | INFO:root:starting session for partitionid-1
sidecar_1  | 2020/04/02 19:06:37 handling get-session for partitionid-1
```

At this point the example client begins sending encrypt and decrypt messages to the server sidecar in a loop and you will see stream of log messages from both the client (myapp_1) and sidecar (sidecar_1). Enter `CTRL+c` to shutdown the application.

**NOTE**: Docker Compose will need to build the images for both containers the first time `docker-compose up` is run on your machine. This process typically takes between 5 to 10 minutes, but build times can vary considerably from one machine to another.

### Kubernetes (kind)
In this example we'll launch the the same multi-container application we used above but this time we'll use [kind](https://kind.sigs.k8s.io/) to spin up a local Kubernetes cluster and `kubectl` to launch the deployment.

#### Prerequisites
Ensure both `kind` and `kubectl` are installed locally.

* [Installing kind](https://kind.sigs.k8s.io/docs/user/quick-start/#installation)
* [Installing kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/)

#### Build and run the example
From the [examples](./examples) directory run `docker build` to build the example Python client image:

```console
[user@machine examples]$ docker build -t example-client clients/python
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
Successfully tagged example-client:latest
```

we'll also need the server image:

```console
[user@machine examples]$ docker build -t asherah-server ../go
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

Now that we have both of the images we can run `kind` to create our local Kubernetes cluster and then load the new images into the cluster:

```console
[user@machine examples]$ kind create cluster
...
[user@machine examples]$ kind load docker-image example-client
...
[user@machine examples]$ kind load docker-image asherah-server
...
```

And now we're ready to use `kubectl` launch the application defined in [deployment.yaml](./examples/deployment.yaml):

```console
[user@machine examples]$ kubectl --context kind-kind apply -f deployment.yaml
deployment.apps/myapp created
```

We can view our pod and the application logs to verify everything is in working order:

```console
[user@machine examples]$ kubectl --context kind-kind get po
NAME                     READY   STATUS    RESTARTS   AGE
myapp-77bd786698-ls42l   2/2     Running   0          70s
[user@machine examples]$ kubectl --context kind-kind logs -c myapp myapp-77bd786698-ls42l | head -n2
INFO:root:starting test
INFO:root:starting session for partitionid-1
```

And when finished, `kind delete cluster` can be used to delete the cluster.

## Server Development

### Code generation
First you'll need to ensure the protobuf compiler is installed...

Then regenerate the gRPC server interfaces for go by running:
```bash
$ protoc --go_out=plugins=grpc:./go protos/appencryption.proto
```
