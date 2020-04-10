# -*- coding: utf-8 -*-

"""Define names for appencryption_pb2 generated types"""


from typing import overload, Any


@overload
class SessionResponse:
    encrypt_response: Any
    decrypt_response: Any


@overload
class SessionRequest:
    encrypt: Any
    decrypt: Any
