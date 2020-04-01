# Asherah Server
Asherah Server is intended to be used by those who wish to utilize application-layer encryption but are unable to take advantage of the SDK directly, e.g., Asherah lacks an implementation in their preferred programming language.

## Overview
Asherah Server is as a light-weight service layer built atop the Asherah SDK with encrypt/decrypt functionality exposed via a [gRPC](https://grpc.io) service. It uses a Unix domain socket for local inter-process communication and is designed to be deployed along side your application.

To integrate with the service you will need to generate client interfaces from the [.proto](../protos/appencryption.proto) service definition. Detailed instructions for generating client code in a number of languages can be found in the [gRPC Quick Starts](https://grpc.io/docs/quickstart/).

Example client implementations as well as multi-container application configurations can be found in the [examples](./examples) directory.

## Development

### Code generation
First you'll need to ensure the protobuf compiler is installed...

Then regenerate the gRPC server interfaces for go by running:
```bash
$ protoc --go_out=plugins=grpc:./go protos/appencryption.proto
```
