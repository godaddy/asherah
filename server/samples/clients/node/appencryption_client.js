/*
Asherah Server gRPC - Node client

A simple Node application that demonstrates integrating with Asherah Server
via a dynamically generated gRPC client.
*/

'use strict';

const yargs = require('yargs');
const grpc = require('@grpc/grpc-js');
const protoLoader = require('@grpc/proto-loader');
const sleep = require('sleep');
const path = require('path');

// Setup logger
const winston = require('winston');
const consoleTransport = new winston.transports.Console();
const myWinstonOptions = {
  transports: [consoleTransport],
  format:
        winston.format.combine(
          winston.format.colorize({ all: true, }),
          winston.format.simple()
        ),
};
const logger = new winston.createLogger(myWinstonOptions);

let socketFile = '';

/**
 * SessionClient provides a synchronous client interface for the bidirectionally-streaming Session
 endpoint.
 */
class SessionClient {
    #call;
    #getSessionResolve;
    #encryptResolve;
    #decryptResolve;

    constructor(appEncryptionProto) {
      let drr;
      let bytes;
      let decryptedPayload;

      const client = new appEncryptionProto.asherah.apps.server.AppEncryption(`unix://${socketFile}`,
        grpc.credentials.createInsecure());

      const self = this;

      const call = client.Session();
      call.on('end', function () {
        logger.info('end of transmission from server');
      });
      call.on('error', function (err) {
        logger.error(err);
      });
      call.on('status', function (status) {
        logger.info(status);
      });
      call.on('data', function (sessionResponse) {
        switch (sessionResponse.response) {
          case 'encrypt_response':
            drr = sessionResponse.encrypt_response.data_row_record;
            logger.info('received DRR');
            self.#encryptResolve(drr);
            break;
          case 'decrypt_response':
            bytes = sessionResponse.decrypt_response.data;
            decryptedPayload = Buffer.from(bytes).toString('utf-8');
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
     * @param {string} partition - The partition id for the session
     * @returns {Promise<void>} - A session_response wrapped in a promise
     */
    getSession(partition) {
      const self = this;
      return new Promise(resolve => {
        self.#getSessionResolve = resolve;

        self.#call.write({ get_session: { partition_id: partition, }, });
      });
    }

    /**
     * Encrypt data using the current session and its partition.
     * @param {string} payload - The payload to be encrypted
     * @returns {Promise<void>} - An encrypt_response wrapped in a promise
     */
    encrypt(payload) {
      const self = this;
      return new Promise(resolve => {
        self.#encryptResolve = resolve;
        self.#call.write({ encrypt: { data: Buffer.from(payload), }, });
      });
    }

    /**
     * Decrypt the data using the current session and its partition
     * @param {bytes[]} drr - An encrypted data row record
     * @returns {Promise<void>} - A decrypt_response wrapped in a promise
     */
    decrypt(drr) {
      const self = this;
      return new Promise(resolve => {
        self.#decryptResolve = resolve;
        // eslint-disable-next-line camelcase
        self.#call.write({ decrypt: { data_row_record: drr, }, });
      });
    }

    /**
     * Close the stream
     */
    close() {
      this.#call.end();
    }
}

/**
 * Generate a random string of fixed length
 * @param {number} length - Length of string to be generated
 * @returns {string} - A random string
 */
function randomString(length = 12) {
  let result = '';
  const characters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  const charactersLength = characters.length;
  for (let i = 0; i < length; i++) {
    result += characters.charAt(Math.floor(Math.random() * charactersLength));
  }

  return result;
}

/**
 * Runs the client
 * @param {SessionClient} client - A session client for the current session
 * @returns {Promise<void>} - Client response wrapped in a promise
 */
async function runClient(client) {
  const payload = randomString();

  logger.info(`encrypting payload ${payload}`);
  const drr = await client.encrypt(payload);

  logger.info(`calling decrypt`);
  const decryptedPayload = await client.decrypt(drr);

  if (decryptedPayload !== payload) {
    logger.info('oh no... something went terribly wrong!');
  } else {
    logger.info(`test completed successfully\n`);
  }
}

/**
 * Executes runClient once
 * @param {SessionClient} client - A session client for the current session
 * @returns {Promise<void>} - Client response wrapped in a promise
 */
async function runOnce(client) {
  await runClient(client);

  client.close();
}

/**
 * Executes runClient until the process is interrupted.
 * @param {SessionClient} client - A session client for the current session
 * @returns {Promise<void>} - Client response wrapped in a promise
 */
async function runContinuously(client) {
  // eslint-disable-next-line no-constant-condition
  while (true) {
    process.on('SIGINT', () => {
      logger.info('received SIGINT. terminating client');
      process.exit(0);
    });
    process.on('SIGTERM', () => {
      logger.info('received SIGTERM. terminating client');
      process.exit(0);
    });
    await runClient(client);

    //  Sleep for 1 second before sending the next request
    sleep.sleep(1);
  }
}

/**
 * Gets a session and calls run_once or run_continuously based on the value of the continuous parameter
 * @param {boolean} continuous - Flag indicating if client should run continuously or not
 * @param {string} id - Unique identifier for the partition
 * @param {GrpcObject} appEncryptionProto - A gRPC object
 * @returns {Promise<void>} - Client response wrapped in a promise
 */
async function run(continuous, id, appEncryptionProto) {
  const client = new SessionClient(appEncryptionProto);

  await client.getSession(`partition-${id}`);

  if (continuous) {
    await runContinuously(client);
  } else {
    await runOnce(client);
  }
}

/**
 * Dynamically load the .proto file with the specified options and subsequently the gRPC package definition
 * @param {string} protoPath - Path of the proto file
 * @returns {GrpcObject} - A gRPC object
 */
function loadProtoDef(protoPath) {
  const appEncryptionDef = protoLoader.loadSync(protoPath, {
    keepCase: true,
    defaults: true,
    oneofs: true,
  });

  const appEncryptionProto = grpc.loadPackageDefinition(appEncryptionDef);

  return appEncryptionProto;
}

function main() {
  const requests = [];
  const argv = yargs.usage('Usage: $0 --socket [string] --continuous [boolean] ' +
        '--num-clients [num] --proto-path [string]')
    .options({
      s: {
        alias: 'socket',
        describe: 'The unix domain socket the server is listening on.',
        type: 'string',
        demand: false,
        default: '/tmp/appencryption.sock',
      },
      c: {
        alias: 'continuous',
        describe: 'When present the client will run a continuous test.',
        type: 'boolean',
        demand: false,
        default: false,
      },
      n: {
        alias: 'num-clients',
        describe: 'The total number of clients to run asynchronously',
        type: 'num',
        demand: false,
        default: 1,
      },
      p: {
        alias: 'proto-path',
        describe: 'The path to the proto file for the service',
        type: 'string',
        demand: false,
        default: path.join(__dirname, '../../../protos/appencryption.proto'),
      },
    })
    .argv;

  socketFile = argv.s;
  logger.info('starting test');

  const appEncryptionProto = loadProtoDef(path.resolve(argv.p));

  for (let i = 1; i <= argv.n; i++) {
    const promise = new Promise(() => run(argv.c, i, appEncryptionProto, () => {
      logger.info('client terminated');
    })
    );
    requests.push(promise);
  }

  Promise.all(requests);
}

if (require.main === module) {
  main();
}
