# gRPC Client (Node) for Asherah Server
A simple client application that demonstrates integrating with Asherah Server via a generated gRPC client.

## Running the client
Ensure the Asherah Server is running locally and listening on `unix:///tmp/appencryption.sock` and run:

```console
[user@machine node]$ node appencryption_client.js
info: starting test
info: encrypting payload BN3bOy1zCKQs
info: received DRR
info: decrypting DRR
info: received decrypted data: BN3bOy1zCKQs
info: test completed successfully
```

## Statically generating the client
Install the client tools

```console
[user@machine node]$ npm install -g grpc grpc-tools
<snip>
[grpc] Success: "/path/to/node/modules/grpc_node.node" is installed via remote
+ grpc-tools@1.8.1
+ grpc@1.24.2
```

Generate the client
```console
[user@machine node]$  grpc_tools_node_protoc --js_out=import_style=commonjs,binary:../node/ \
 -I../../../protos  --grpc_out=.  --plugin=protoc-gen-grpc=`which grpc_tools_node_protoc_plugin` \
../../../protos/appencryption.proto
```

**NOTE** The sample application does not support using static codegen. More information about using static codegen can be
found [here](https://github.com/grpc/grpc/tree/v1.28.1/examples/node/static_codegen/route_guide)

## Configuring the client
The sample client can be configured using command-line arguments. Supported options are as follows:

```
Usage: appencryption_client.js --socket [string] --continuous [boolean]
--num-clients [num] --proto-path [string]

Options:
  --help             Show help                                         [boolean]
  --version          Show version number                               [boolean]
  -s, --socket       The unix domain socket the server is listening on.
                                   [string] [default: "/tmp/appencryption.sock"]
  -c, --continuous   When present the client will run a continuous test.
                                                      [boolean] [default: false]
  -n, --num-clients  The total number of clients to run asynchronously
                                                                    [default: 1]
  -p, --proto-path   The path to the proto file for the service
                    [string] [default: "../../../../protos/appencryption.proto"]
```

## Development Notes
To build the docker image locally, the `docker build -f samples/clients/node/Dockerfile .` command needs to be executed
from the [server](/server) directory. Since the node client generates the proto code dynamically, we need to modify the
build context to pass the [proto file](../../../protos/appencryption.proto) during the build phase.
