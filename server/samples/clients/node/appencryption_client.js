/*
Asherah Server gRPC - NodeJS client

A simple NodeJS application that demonstrates integrating with Asherah Server
via a dynamically generated gRPC client.
*/

'use strict';

const async = require('async');
const yargs = require('yargs');
let call;

// Dynamically generate the protobuf code
const grpc = require('@grpc/grpc-js');
const protoLoader = require('@grpc/proto-loader');
const appEncryptionDef = protoLoader.loadSync(__dirname + '../../../../protos/appencryption.proto', {
    keepCase: true,
    defaults: true,
    oneofs: true
});
const appEncryptionProto = grpc.loadPackageDefinition(appEncryptionDef);

// Setup logger
const winston = require('winston');
const consoleTransport = new winston.transports.Console();
const myWinstonOptions = {
    transports: [consoleTransport],
    format: winston.format.combine(
        winston.format.colorize({ all: true }),
        winston.format.simple()
    )
};
const logger = new winston.createLogger(myWinstonOptions);

/**
 * Generate a random string of fixed length
 * @param length
 * @returns {string}
 */
function randomString(length = 12) {
    let result = '';
    let characters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    let charactersLength = characters.length;
    for (let i = 0; i < length; i++) {
        result += characters.charAt(Math.floor(Math.random() * charactersLength));
    }
    return result;
}

/**
 * Create a new session for communicating with the gRPC server
 * @param socket
 */
function getSession(socket, callback) {
    let client = new appEncryptionProto.asherah.apps.server.AppEncryption(`unix://${socket}`, grpc.credentials.createInsecure());
    call = client.session();
    call.write({get_session: {partition_id: 'partition-1'}});
    return callback(null);
}

/**
 * Encrypt data using the current session and its partition.
 * Executes a callback to pass the encrypted value (when received) to  decrypt for further processing
 * @param callback
 */
function encrypt(callback) {
    let payload = randomString();
    logger.info(`encrypting payload ${payload}`);
    call.write({encrypt: {data: Buffer.from(payload)}});
    call.on('data', function (sessionResponse) {
        if (sessionResponse.response === 'encrypt_response') {
            logger.info(`received DRR`);
            let drr = sessionResponse.encrypt_response.data_row_record;
            return callback(null, payload, drr);
        }
    });
}

/**
 * Decrypt the data using the current session and its partition
 * @param payload
 * @param drr
 * @param callback
 */
function decrypt(payload, drr, callback) {
    call.write({decrypt: {data_row_record: drr}});

    call.on('data', function (sessionResponse) {
        if (sessionResponse.response === 'decrypt_response') {
            logger.info(`decrypting DRR`);
            let bytes = sessionResponse.decrypt_response.data;
            let decryptedPayload = Buffer.from(bytes).toString('utf-8');
            logger.info(`received decrypted data: ${decryptedPayload}`);
            if (decryptedPayload != payload) {
                return callback(null, new Error('oh no... something went terribly wrong!'))
            }
            return callback(null, 'test completed successfully');
        }
    });

}

/**
 * Executes the client
 * @param runOnce
 */
function run(socket) {
    async.waterfall([
        async.apply(getSession, socket),
        encrypt,
        decrypt,
    ], function (err, res) {
        logger.info(`${res}\n`);
            call.end();
    });
}

/**
 * Executes run once
 */
function run_once(socket) {
    run(socket);
}

/**
 * Executes run until the process is interrupted.
 */
function run_continuously(socket) {
    async.whilst(
        function test(cb) {
            cb(null, true);
        },
        function iter(callback) {
            run(socket);
            setTimeout(function () {
                callback(null);
            }, 1000);
        },
        function () {
        }
    );
}

/**
 * Run the client
 */
function main() {
    let argv = yargs.usage('Usage: $0 --socket [string] --continuous [boolean]')
        .options({
            's': {
                alias: 'socket',
                describe: 'The unix domain socket the server is listening on.',
                type: 'string',
                demand: false,
                default: '/tmp/appencryption.sock'
            },
            'c': {
                alias: 'continuous',
                describe: 'When present the client will run a continuous test.',
                type: 'boolean',
                demand: false,
                default: false,
            }
        })
        .argv;

    logger.info('starting test');

    if (!argv.c) {
        run_once(argv.s);
    } else {
        run_continuously(argv.s);
    }

}

if (require.main === module) {
    main();
}
