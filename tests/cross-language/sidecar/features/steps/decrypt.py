# -*- coding: utf-8 -*-

"""Decrypt feature definitions
"""

import base64
import json

from behave import given, when, then
from appencryption_client import SessionClient

import appencryption_pb2


@given(u'I have encrypted_data from "{filename}"')
def step_impl(context, filename):
    assert filename != ''
    context.filename = '/tmp/' + filename
    f = open(context.filename, 'r')
    context.drr = f.read()


@when(u'I decrypt the encrypted_data')
def step_impl(context):
    with SessionClient('/tmp/appencryption.sock', 'partition') as client:
        encrypted_json = json.loads(base64.b64decode(context.drr))

        drr = appencryption_pb2.DataRowRecord()
        drr.data = base64.b64decode(encrypted_json['Data'])
        drr.key.created = encrypted_json['Key']['Created']
        drr.key.key = base64.b64decode(encrypted_json['Key']['Key'])
        drr.key.parent_key_meta.key_id = encrypted_json['Key']['ParentKeyMeta']['KeyId']
        drr.key.parent_key_meta.created = encrypted_json['Key']['ParentKeyMeta']['Created']

        context.decryptedPayload = client.decrypt(drr)


@then(u'I should get decrypted_data')
def step_impl(context):
    assert context.decryptedPayload != ''


@then(u'decrypted_data should be equal to "{data}"')
def step_impl(context, data):
    assert context.decryptedPayload.decode() == data
