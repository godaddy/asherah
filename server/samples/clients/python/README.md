# gRPC Client (Python) for Asherah Server
A simple client application that demonstrates integrating with Asherah Server via a generated gRPC client.

## Running the client
Ensure the Asherah Server is running locally and listening on `unix:///tmp/appencryption.sock` and run:

```console
[user@machine python]$ python appencryption_client.py
INFO:root:starting test
INFO:root:starting session for partitionid-1
INFO:root:encrypting: b'my "secret" data'
INFO:root:received DRR
INFO:root:decrypting DRR
INFO:root:received decrypted data: b'my "secret" data'
INFO:root:test completed successfully
```

## (Re)generating the client
Create a python virtual environment and install the client tools

```console
[user@machine python]$ python3 -m venv .venv
[user@machine python]$ source .venv/bin/activate
[user@machine python]$ pip install --upgrade pip
[user@machine python]$ pip install grpcio-tools
<snip>
Successfully installed grpcio-1.27.2 grpcio-tools-1.27.2 protobuf-3.11.3 six-1.14.0
```

Generate the client
```console
[user@machine python]$ python -m grpc_tools.protoc \
    -I../../../protos \
    --python_out=. \
    --grpc_python_out=. \
    ../../../protos/appencryption.proto
```

## Configuring the client
The sample client can be configured using command-line arguments. Supported options are as follows:

```
usage: appencryption_client.py [-h] [--socket SOCKET]
                               [--num-clients NUM_CLIENTS] [--continuous]

Connect to a local Asherah Server and execute a series of operations.

optional arguments:
  -h, --help            show this help message and exit
  --socket SOCKET       The unix domain socket the server is listening on.
  --num-clients NUM_CLIENTS
                        The total number of clients to run concurrently.
  --continuous          When present the client will run a continuous test.
```
