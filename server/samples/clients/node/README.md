# gRPC Client (Node) for Asherah Server
A simple client application that demonstrates integrating with Asherah Server via a generated gRPC client.

## Running the client
Ensure the Asherah Server is running locally and listening on `unix:///tmp/appencryption.sock` and run:

```console
[user@machine node]$ node appencryption_client.py
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
[grpc] Success: "/usr/local/lib/node_modules/grpc/src/node/extension_binary/node-v79-darwin-x64-unknown/grpc_node.node" is installed via remote
+ grpc-tools@1.8.1
+ grpc@1.24.2

```

Generate the client
```console
[user@machine python]$  grpc_tools_node_protoc --js_out=import_style=commonjs,binary:../node/ \
 -I../../../protos  --grpc_out=.  --plugin=protoc-gen-grpc=`which grpc_tools_node_protoc_plugin` \
../../../protos/appencryption.proto
```

## Configuring the client
The sample client can be configured using command-line arguments. Supported options are as follows:

```
Usage: appencryption_client.js --socket [string] --continuous [boolean] --num-clients [num]

Options:
  --help             Show help                                         [boolean]
  --version          Show version number                               [boolean]
  -s, --socket       The unix domain socket the server is listening on.
                                   [string] [default: "/tmp/appencryption.sock"]
  -c, --continuous   When present the client will run a continuous test.
                                                      [boolean] [default: false]
  -n, --num-clients  The total number of clients to run asynchronously
                                                                    [default: 1]

```
