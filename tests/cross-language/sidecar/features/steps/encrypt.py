# -*- coding: utf-8 -*-

"""Encrypt feature definitions
"""

import base64
import json
import os

from behave import given, when, then
from google.protobuf.json_format import MessageToDict

from appencryption_client import SessionClient


@given(u'I have "{data}"')
def step_impl(context, data):
    assert data != ""
    context.payloadString = data


@when(u'I encrypt the data')
def step_impl(context):
    with SessionClient('/tmp/appencryption.sock', 'partition') as client:
        server_drr = client.encrypt(context.payloadString.encode())
        data_row_record = MessageToDict(server_drr)
        parent_key_meta_json = {'KeyId': data_row_record['key']['parentKeyMeta']['keyId'],
                                'Created': int(data_row_record['key']['parentKeyMeta']['created'])}

        key_json = {'ParentKeyMeta': parent_key_meta_json,
                    'Key': data_row_record['key']['key'],
                    'Created': int(data_row_record['key']['created'])}

        drr_json = {'Data': data_row_record['data'],
                    'Key': key_json}

        json_str = json.dumps(drr_json)
        encoded_bytes = base64.b64encode(json_str.encode('utf-8'))
        context.drr = encoded_bytes.decode()


@then(u'I should get encrypted_data')
def step_impl(context):
    assert context.drr != ''
    file_path = context.config.userdata['FILE']

    # Remove the file if it already exists
    if os.path.exists(file_path):
        os.remove(file_path)
    f = open(file_path, 'w')
    f.write(str(context.drr))
    f.close()


@then(u'encrypted_data should not be equal to data')
def step_impl(context):
    assert context.payloadString != context.drr
