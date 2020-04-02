# Asherah Server
Asherah Server is intended to be used by those who wish to utilize application-layer encryption but are unable to take advantage of the SDK directly, e.g., Asherah lacks an implementation in their preferred programming language.

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

> Note: Docker Compose will need to build the images for both containers the first time `docker-compose up` is run on your machine. This process typically takes between 5 to 10 minutes, but build times can vary considerably from one machine to another.

### Kubernetes (kind)
Coming soon...

## Server Development

### Code generation
First you'll need to ensure the protobuf compiler is installed...

Then regenerate the gRPC server interfaces for go by running:
```bash
$ protoc --go_out=plugins=grpc:./go protos/appencryption.proto
```
