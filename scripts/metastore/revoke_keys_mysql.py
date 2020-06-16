#!/usr/bin/env python3

# Until this gets put into a proper deployment model, setup to run this is:
#
# python3 -m virtualenv venv
# source venv/bin/activate
# pip install mysql-connector

import argparse
import json
import logging
import mysql.connector
import os
import time

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

SELECT_BY_KEY_QUERY = "SELECT id, created, key_record FROM encryption_key WHERE id = %s AND created = %s"
SELECT_BY_CREATED_BEFORE_AND_ID_PREFIX_QUERY = "SELECT id, created, key_record FROM encryption_key WHERE created < %s AND id LIKE %s"
UPDATE_KEY_RECORD_BY_KEY_QUERY = "UPDATE encryption_key SET key_record = %s WHERE id = %s AND created = %s"


def revoke_envelope_key_record_by_key(connection, execute_flag, id, created):
    cursor = connection.cursor(dictionary=True)
    try:
        cursor.execute(SELECT_BY_KEY_QUERY, (id, created))
        row = cursor.fetchone()

        if row:
            logger.info('Found envelope key record for id={}, created={}. Revoking...'.format(id, created))

            envelope_key_record_json = json.loads(row['key_record'])
            envelope_key_record_json['Revoked'] = True

            if execute_flag:
                cursor.execute(UPDATE_KEY_RECORD_BY_KEY_QUERY, (json.dumps(envelope_key_record_json, separators=(',', ":")), id, created))
                connection.commit()

                logger.info('Marked {} keys revoked successfully!'.format(cursor.rowcount))
            else:
                envelope_key_record_json['Key'] = '<REDACTED>'
                logger.info('DRY-RUN query={}'.format(UPDATE_KEY_RECORD_BY_KEY_QUERY %
                                                      (json.dumps(envelope_key_record_json, separators=(',', ":")), id, created)))
        else:
            logger.warning('Envelope key record for id={}, created={} not found!'.format(id, created))
    finally:
        cursor.close()


def revoke_intermediate_keys_by_created(connection, execute_flag, created):
    return revoke_envelope_key_records_by_created_and_id_prefix(connection, execute_flag, created, "_IK_")


def revoke_system_keys_by_created(connection, execute_flag, created):
    return revoke_envelope_key_records_by_created_and_id_prefix(connection, execute_flag, created, "_SK_")


def revoke_envelope_key_records_by_created_and_id_prefix(connection, execute_flag, created, id_prefix):
    cursor = connection.cursor(dictionary=True)
    try:
        cursor.execute(SELECT_BY_CREATED_BEFORE_AND_ID_PREFIX_QUERY, (created, '{}%'.format(id_prefix)))
        # TODO For some reason, rowcount comes back as -1. Wonder if it appears after a fetch?
        logger.info('Fetched {} rows to revoke using id_prefix={}, created<{}'.format(cursor.rowcount, id_prefix, created))

        update_tuples = []
        for row in iter_row(cursor, 100):
            envelope_key_record_json = json.loads(row['key_record'])
            # Only update if not already revoked and handle missing Revoked flag
            if not envelope_key_record_json.get('Revoked') or not envelope_key_record_json['Revoked']:
                envelope_key_record_json['Revoked'] = True

                # Note we're appending a tuple
                update_tuples.append((json.dumps(envelope_key_record_json, separators=(',', ":")), row['id'], row['created']))

        if update_tuples:
            if execute_flag:
                # Apparently this may not actually be optimized to a bulk update currently, but maybe it will be someday?
                cursor.executemany(UPDATE_KEY_RECORD_BY_KEY_QUERY, update_tuples)
                logger.info('Marked {} keys revoked successfully!'.format(cursor.rowcount))
                connection.commit()
            else:
                logger.info('DRY-RUN would have run query={} for {} keys'.format(UPDATE_KEY_RECORD_BY_KEY_QUERY, len(update_tuples)))
    finally:
        cursor.close()


def iter_row(cursor, size=100):
    while True:
        rows = cursor.fetchmany(size)
        if not rows:
            break
        for row in rows:
            yield row


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Revoke script for JDBC metastore (MySQL only currently). NOTE: Will perform dry-run by default.')
    parser.add_argument('--execute', action='store_true',
                        help='Executes any write operations, which do not run by default (read operations always execute)')
    parser.add_argument('--host', required=True)
    parser.add_argument('--port', required=False, default=3306, type=int)
    parser.add_argument('--database', required=True)
    parser.add_argument('--user', required=True)
    parser.add_argument('--password', required=True)

    subparsers = parser.add_subparsers(title='actions', dest='action')
    subparsers.required = True

    single_parser = subparsers.add_parser('single', help='Revoke a single key by id and created time')
    single_parser.add_argument('--id', required=True, help='The key id')
    single_parser.add_argument('--created', required=True, help='The key created time')

    bulk_parser = subparsers.add_parser('bulk', help='Revoke all system or intermediate keys created before the given time')
    bulk_parser.add_argument('--created-before', required=True, help='The created time cutoff')
    bulk_parser.add_argument('--type', required=True, choices=('system', 'intermediate'), help='The type of keys to revoke')

    arguments = parser.parse_args()

    execute_flag = arguments.execute

    connection = mysql.connector.connect(host=arguments.host, port=arguments.port, database=arguments.database,
                                         user=arguments.user, password=arguments.password,)
    try:
        if arguments.action == 'single':
            revoke_envelope_key_record_by_key(connection, execute_flag, arguments.id, arguments.created)
        else:
            # bulk action
            if arguments.type == 'system':
                revoke_system_keys_by_created(connection, execute_flag, arguments.created_before)
            else:
                revoke_intermediate_keys_by_created(connection, execute_flag, arguments.created_before)
    finally:
        connection.close()
