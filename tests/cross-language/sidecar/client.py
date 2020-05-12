# -*- coding: utf-8 -*-

"""Asherah Server gRPC - Python client
"""
import queue
from types import TracebackType
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

    def __init__(self) -> None:
        self.requests = queue.Queue()

        self.channel = grpc.insecure_channel(f'unix:///tmp/appencryption.sock')
        self.stub = appencryption_pb2_grpc.AppEncryptionStub(self.channel)
        self.partition = 'partition'

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

    def _send_receive(self, req: SessionRequest) -> SessionResponse:
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
        req = appencryption_pb2.SessionRequest()
        req.encrypt.data = data

        resp = self._send_receive(req)

        return resp.encrypt_response.data_row_record

    def decrypt(self, drr: Any) -> bytes:
        req = appencryption_pb2.SessionRequest()
        req.decrypt.data_row_record.CopyFrom(drr)

        resp = self._send_receive(req)

        return resp.decrypt_response.data
