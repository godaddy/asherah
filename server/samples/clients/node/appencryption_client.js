/*
Asherah Server gRPC - NodeJS client

A simple NodeJS application that demonstrates integrating with Asherah Server
via a dynamically generated gRPC client.
*/

'use strict';

const async = require('async');
const yargs = require('yargs');

// Dynamically generate the protobuf code
const grpc = require('@grpc/grpc-js');
const protoLoader = require('@grpc/proto-loader');
const appEncryptionDef = protoLoader.loadSync(
    __dirname + '../../../../protos/appencryption.proto',
    {
        keepCase: true,
        defaults: true,
        oneofs: true,
    });
const appEncryptionProto = grpc.loadPackageDefinition(appEncryptionDef);

// Setup logger
const winston = require('winston');
const consoleTransport = new winston.transports.Console();
const myWinstonOptions = {
    transports: [consoleTransport],
    format:
        winston.format.combine(
            winston.format.colorize({all: true,}),
            winston.format.simple()
        ),
};
const logger = new winston.createLogger(myWinstonOptions);

let requests = [];

/**
 * SessionClient provides a synchronous client interface for the bidirectionally-streaming Session
 endpoint.
 */
class SessionClient {
    constructor(socket, partition) {
        let client = new appEncryptionProto.asherah.apps.server.AppEncryption(
            `unix://${socket}`,
            grpc.credentials.createInsecure()
        );

        this.call = client.session();
        this.call.on('error', function (err) {
            logger.error(err);
            process.exit(2);
        });

        this.partition = partition
    }

    /**
     * Create a new session for communicating with the gRPC server
     * @param socket
     */
    getSession(sessionClient, callback) {
        logger.info('initializing session');
        this.call.write({get_session: {partition_id: this.partition,},});
    }

    /**
     * Encrypt data using the current session and its partition.
     * Executes a callback to pass the encrypted value (when received) to  decrypt for further processing
     * @param callback
     */
    encrypt(sessionClient, callback) {
        let payload = randomString();
        logger.info(`encrypting payload ${payload}`);
        sessionClient.call.write({encrypt: {data: Buffer.from(payload),},});

        sessionClient.call.on('data', function (sessionResponse) {
            if (sessionResponse.response === 'encrypt_response') {
                let drr = sessionResponse.encrypt_response.data_row_record;
                logger.info(`received DRR`);

                return callback(null, sessionClient, payload, drr);
            }
        });
    }

    /**
     * Decrypt the data using the current session and its partition
     * @param payload
     * @param drr
     * @param callback
     */
    decrypt(sessionClient, payload, drr, callback) {
        sessionClient.call.write({decrypt: {data_row_record: drr,},});

        sessionClient.call.on('data', function (sessionResponse) {
            if (sessionResponse.response === 'decrypt_response') {
                logger.info(`decrypting DRR`);
                let bytes = sessionResponse.decrypt_response.data;
                let decryptedPayload = Buffer.from(bytes).toString('utf-8');
                logger.info(`received decrypted data: ${decryptedPayload}`);

                if (decryptedPayload !== payload) {
                    return callback(null, new Error('oh no... something went terribly wrong!'))
                }

                return callback(null, 'test completed successfully');
            }
        });
    }
}

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
 * Runs the client
 * @param sessionClient
 * @param runOnce
 */
function run_client(sessionClient, runOnce = true) {
    async.waterfall([
        async.apply(sessionClient.encrypt, sessionClient),
        sessionClient.decrypt
    ], function (err, res) {
        logger.info(`${res}\n`);
        // if (runOnce) {
        sessionClient.call.end();
        // }
    });
}

/**
 * Executes run_client once
 */
function run_once(sessionClient) {

    run_client(sessionClient);
}

/**
 * Executes run_client until the process is interrupted.
 */
function run_continuously(socket, partition) {
    let count = 0;
    async.whilst(
        function test(callback) {
            callback(null, true);
        },
        function iterate(callback) {
            let sessionClient = new SessionClient(socket, partition);
            sessionClient.getSession();
            run_client(sessionClient, false);
            setTimeout(function () {
                callback(null);
            }, 1000);
        },
        function () {
            // Do nothing since this is an  infinite loop
        }
    );
}

function run(continuous, socket, partitionId) {
    let partition = `partitionid-${partitionId}`;
    if (continuous) {
        run_continuously(socket, partition);
    } else {
        let sessionClient = new SessionClient(socket, partition);
        sessionClient.getSession();
        run_once(sessionClient);
    }
}

/**
 * Run the client
 */
function main() {
    let argv = yargs.usage('Usage: $0 --socket [string] --continuous [boolean] --num-clients [num')
        .options({
            's': {
                alias: 'socket',
                describe: 'The unix domain socket the server is listening on.',
                type: 'string',
                demand: false,
                default: '/tmp/appencryption.sock',
            },
            'c': {
                alias: 'continuous',
                describe: 'When present the client will run a continuous test.',
                type: 'boolean',
                demand: false,
                default: false,
            },
            'n': {
                alias: 'num-clients',
                describe: 'The total number of clients to run asynchronously',
                type: 'num',
                demand: false,
                default: 1,
            },
        })
        .argv;
    logger.info('starting test');

    for (let i = 1; i <= argv.n; i++) {
        let promise = new Promise(() => run(argv.c, argv.s, i));
        requests.push(promise);
    }


    Promise.all(requests).then(function () {
        console.log("DONE")
    });
}

if (require.main === module) {
    main();
}
