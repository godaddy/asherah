# Asherah Server
Asherah Server is intended to be used by those who wish to utilize application level encryption but are unable to take advantage of the SDK directly, e.g., Asherah lacks an implementation in their preferred programming language.

## Development

### Code generation
First you'll need to ensure the protobuf compiler is installed...

Then regenerate the gRPC server interfaces for go by running:
```bash
$ protoc --go_out=plugins=grpc:./go protos/appencryption.proto
```
