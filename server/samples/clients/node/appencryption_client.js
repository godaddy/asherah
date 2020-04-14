/*
Asherah Server gRPC - NodeJS client

A simple NodeJS application that demonstrates integrating with Asherah Server
via a dynamically generated gRPC client.
*/

'use strict';

const yargs = require('yargs');
const grpc = require('@grpc/grpc-js');
const protoLoader = require('@grpc/proto-loader');

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
    #call;
    #getSessionResolve;
    #encryptResolve;
    #decryptResolve;

    constructor(callback) {
        let appEncryptionDef = protoLoader.loadSync(__dirname + '../../../../protos/appencryption.proto', {
            keepCase: true,
            defaults: true,
            oneofs: true
        });

        let appEncryptionProto = grpc.loadPackageDefinition(appEncryptionDef);
        let client = new appEncryptionProto.asherah.apps.server.AppEncryption('unix:///tmp/appencryption.sock', grpc.credentials.createInsecure());

        let call = client.Session();
        call.on('end', function () {
            logger.info('end');
            callback();
        });
        call.on('error', function (err) {
            logger.error(err);
        });
        call.on('status', function (status) {
            logger.info(status);
        });

        let self = this;
        call.on('data', function (sessionResponse) {
            switch (sessionResponse.response) {
                case 'encrypt_response':
                    let drr = sessionResponse.encrypt_response.data_row_record;
                    logger.info('received DRR');
                    self.#encryptResolve(drr);
                    break;
                case 'decrypt_response':
                    let bytes = sessionResponse.decrypt_response.data;
                    let decryptedPayload = Buffer.from(bytes).toString('utf-8');
                    logger.info(`received decrypted data: ${decryptedPayload}`);
                    self.#decryptResolve(decryptedPayload);
                    break;
                case 'error_response':
                    logger.info('error received: ' + sessionResponse.error_response.message);
                    break;
                default:
                    self.#getSessionResolve();
            }
        });

        this.#call = call;
    }


    /**
     * Create a new session for communicating with the gRPC server
     * @param socket
     */
    getSession(partition) {
        let self = this;
        return new Promise(function (resolve, err) {
            self.#getSessionResolve = resolve;

            self.#call.write({get_session: {partition_id: partition}});
        });
    }

    /**
     * Encrypt data using the current session and its partition.
     * Executes a callback to pass the encrypted value (when received) to  decrypt for further processing
     * @param callback
     */
    encrypt(payload) {
        let self = this;
        return new Promise(function (resolve, err) {
            self.#encryptResolve = resolve;
            self.#call.write({encrypt: {data: Buffer.from(payload)}});
        });
    }

    /**
     * Decrypt the data using the current session and its partition
     * @param payload
     * @param drr
     * @param callback
     */
    decrypt(drr) {
        let self = this;
        return new Promise(function (resolve, err) {
            self.#decryptResolve = resolve;
            self.#call.write({decrypt: {data_row_record: drr}});
        });
    }

    close() {
        this.#call.end()
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
async function run_client(client) {
    let payload = randomString();

    logger.info(`encrypting payload ${payload}`);
    let drr = await client.encrypt(payload);

    logger.info(`calling decrypt`);
    let decryptedPayload = await client.decrypt(drr);

    if (decryptedPayload != payload) {
        logger.info('oh no... something went terribly wrong!');
    } else {
        logger.info(`test completed successfully\n`);
    }
}

/**
 * Executes run_client once
 */
async function run_once(client) {

    await run_client(client);
    client.close();
}

/**
 * Executes run_client until the process is interrupted.
 */
async function run_continuously(client) {
    while (true) {
        await run_client(client);
    }
}

async function run(continuous, id, callback) {
    const client = new SessionClient(callback);

    logger.info('awaiting session');
    await client.getSession(`partition-${id}`);

    if (continuous) {
        await run_continuously(client);
    } else {
        await run_once(client);
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
        let promise = new Promise(() => run(argv.c, i));
        requests.push(promise);
    }

    Promise.all(requests);
}

if (require.main === module) {
    main();
}
