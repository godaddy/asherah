import base64
import json

from behave import *

import appencryption_pb2
from client import SessionClient


@given(u'I have encrypted_data from {filename}')
def step_impl(context, filename):
    assert filename != ''
    context.filename = '/tmp/' + filename.strip('\"')
    f = open(context.filename, 'r')
    context.drr = f.read()


@when(u'I decrypt the encrypted_data')
def step_impl(context):
    with SessionClient() as client:
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


@then(u'decrypted_data should be equal to {data}')
def step_impl(context, data):
    assert context.decryptedPayload.decode() == data.strip('\"')
