"""Ashera Server gRPC - Python client

A simple Python application that demonstrates integrating with Asherah Server
via a generated gRPC client.
"""

import argparse
import logging
import queue
import sys

import grpc

import appencryption_pb2
import appencryption_pb2_grpc


class SessionReceiveError(Exception):
    """Raised when a gRPC error message is received by the client."""


class SessionClient:
    """A synchronous Session wrapper

    SessionClient provides a synchronous client interface for the bidirectionally-streaming Session
    endpoint.
    """

    def __init__(self, socket: str, partition: str):
        self.requests = queue.Queue()

        self.channel = grpc.insecure_channel(f'unix://{socket}')
        self.stub = appencryption_pb2_grpc.AppEncryptionStub(self.channel)
        self.partition = partition

        self.session = None

    def __enter__(self):
        self._enter()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self._close()
        return False

    def _close(self):
        self.channel.close()

    def _enter(self):
        req = appencryption_pb2.SessionRequest()
        req.get_session.partition_id = self.partition

        self._send_receive(req)

    def _send_receive(
            self,
            req: appencryption_pb2.SessionRequest
    ) -> appencryption_pb2.SessionResponse:
        self.requests.put(req)

        if self.session is None:
            self.session = self.stub.Session(self._next_request())

        resp = next(self.session)
        if resp.HasField('error_response'):
            raise SessionReceiveError(resp.error_response.message)

        return resp

    def _next_request(self):
        while True:
            yield self.requests.get()

    def encrypt(self, data: bytes) -> appencryption_pb2.DataRowRecord:
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

    def decrypt(self, drr: appencryption_pb2.DataRowRecord) -> bytes:
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


def run_client_test(client: SessionClient):
    """Initiate a Asherah Server session, encrypt sample data, decrypt the DRR, then compare
    the decrypted data to the original.
    """

    secret = b'my "secret" data'

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


def _main():
    parser = argparse.ArgumentParser(
        description='Connect to a local Asherah Server and execute a series of operations.')
    parser.add_argument(
        '--socket',
        default='/tmp/appencryption.sock',
        help='The unix domain socket the server is listening on')
    args = parser.parse_args()

    logging.info('starting test')

    partition = 'partitionid-1'

    logging.info('starting session for %s', partition)
    with SessionClient(socket=args.socket, partition=partition) as client:
        run_client_test(client)


if __name__ == '__main__':
    logging.basicConfig(stream=sys.stdout, level=logging.DEBUG)
    _main()
