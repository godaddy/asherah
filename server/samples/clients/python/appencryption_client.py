# -*- coding: utf-8 -*-

"""Asherah Server gRPC - Python client

A simple Python application that demonstrates integrating with Asherah Server
via a generated gRPC client.
"""

import argparse
import asyncio
import logging
import queue
import random
import signal
import string
import sys
from types import FrameType, TracebackType
from typing import Any, Iterator, Optional, Type

import grpc
import appencryption_pb2
import appencryption_pb2_grpc
from appencryption_types import SessionRequest, SessionResponse


class SessionReceiveError(Exception):
    """Raised when a gRPC error message is received by the client."""


class SessionClient:
    """A synchronous Session wrapper

    SessionClient provides a synchronous client interface for the bidirectionally-streaming Session
    endpoint.
    """
    requests: 'queue.Queue[SessionRequest]' = queue.Queue()
    session: Optional[Iterator]

    def __init__(self, socket: str, partition: str) -> None:
        self.requests = queue.Queue()

        self.channel = grpc.insecure_channel(f'unix://{socket}')
        self.stub = appencryption_pb2_grpc.AppEncryptionStub(self.channel)
        self.partition = partition

        self.session = None

    def __enter__(self) -> 'SessionClient':
        self._enter()
        return self

    def __exit__(
            self,
            exc_type: Optional[Type[BaseException]],
            exc_value: Optional[BaseException],
            traceback: Optional[TracebackType]
    ) -> None:
        self._close()

    def _close(self) -> None:
        self.channel.close()

    def _enter(self) -> None:
        req = appencryption_pb2.SessionRequest()
        req.get_session.partition_id = self.partition

        self._send_receive(req)

    def _send_receive(
            self,
            req: SessionRequest,
    ) -> SessionResponse:
        self.requests.put(req)

        if self.session is None:
            self.session = self.stub.Session(self._next_request())

        resp = next(self.session)
        if resp.HasField('error_response'):
            raise SessionReceiveError(resp.error_response.message)

        return resp

    def _next_request(self) -> Iterator[SessionRequest]:
        while True:
            yield self.requests.get()

    def encrypt(self, data: bytes) -> Any:
        """Encrypt data using the current session and its partition.

        Args:
            data: A bytes object containing the data to be encrypted.
        Returns:
            An appencryption_pb2.DataRowRecord containing the encrypted data,
            as well as its encrypted key and metadata.
        """
        req = appencryption_pb2.SessionRequest()
        req.encrypt.data = data

        resp = self._send_receive(req)

        return resp.encrypt_response.data_row_record

    def decrypt(self, drr: Any) -> bytes:
        """Decrypt the data using the current session and its partition.

        Args:
            drr: An appencryption_pb2.DataRowRecord containing the data to be decrypted.
        Returns:
            The decrypted data as bytes.
        """
        req = appencryption_pb2.SessionRequest()
        req.decrypt.data_row_record.CopyFrom(drr)

        resp = self._send_receive(req)

        return resp.decrypt_response.data


class InterruptHandler:
    """A simple signal handler."""

    def __init__(self) -> None:
        self.interrupted = False

    # pylint: disable=unused-argument
    def _interrupt(self, sig: int, frame: FrameType):
        logging.info('received signal %s', signal.Signals(sig).name)
        self.interrupted = True

    def start(self) -> None:
        """Start handling signals."""
        signal.signal(signal.SIGINT, self._interrupt)
        signal.signal(signal.SIGQUIT, self._interrupt)
        signal.signal(signal.SIGTERM, self._interrupt)


async def run_once(client: SessionClient) -> None:
    """Executes run_client_test once."""

    run_client_test(client)


async def run_continuously(client: SessionClient, handler: InterruptHandler) -> None:
    """Executes run_client_test until the process is interrupted."""

    while True:
        run_client_test(client)

        if handler.interrupted:
            break

        # now sleep for random duration between .5 to 1 second
        await asyncio.sleep(random.randrange(5, 11)/10)


def random_string(length: int = 12) -> str:
    """Generate a random string of fixed length."""

    letters = string.ascii_lowercase
    return ''.join(random.choice(letters) for i in range(length))


def run_client_test(client: SessionClient):
    """Initiate a Asherah Server session, encrypt sample data, decrypt the DRR, then compare
    the decrypted data to the original.
    """

    secret = f'my "secret" data - {random_string()}'.encode()

    logging.info('encrypting: %s', secret)
    drr = client.encrypt(secret)
    logging.info('received DRR')

    logging.info('decrypting DRR')
    data = client.decrypt(drr)
    logging.info('received decrypted data: %s', data)

    if secret != data:
        logging.info('oh no... something went terribly wrong!')
        return

    logging.info('test completed successfully')


async def _run_client(
        socket_file: string,
        partition_id: int,
        continuous: bool,
        handler: InterruptHandler
) -> None:
    partition = f'partitionid-{partition_id}'

    logging.info('starting session for %s', partition)
    with SessionClient(socket_file, partition) as client:
        if continuous:
            await run_continuously(client, handler)
        else:
            await run_once(client)


async def _main():
    parser = argparse.ArgumentParser(
        description='Connect to a local Asherah Server and execute a series of operations.')
    parser.add_argument(
        '--socket',
        default='/tmp/appencryption.sock',
        help='The unix domain socket the server is listening on.')
    parser.add_argument(
        '--num-clients',
        default=1,
        type=int,
        help='The total number of clients to run concurrently.')
    parser.add_argument(
        '--continuous',
        action='store_true',
        help='When present the client will run a continuous test.')
    args = parser.parse_args()

    handler = InterruptHandler()
    handler.start()

    logging.info('starting test')
    tasks = {asyncio.create_task(_run_client(args.socket, i, args.continuous, handler))
             for i in range(1, args.num_clients+1)}
    await asyncio.wait(tasks)


if __name__ == '__main__':
    logging.basicConfig(stream=sys.stdout, level=logging.DEBUG)
    asyncio.run(_main())
