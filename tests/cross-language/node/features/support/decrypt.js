const { Given, When, Then } = require('@cucumber/cucumber')
const assert = require('assert');
const asherah = require("asherah");
const fs = require("fs");

const fileDirectory = "/tmp/";

var encryptedPayload;
var decryptedPayload;

var adoDatabaseName = process.env.TEST_DB_NAME;
var adoUsername = process.env.TEST_DB_USER;
var adoPassword = process.env.TEST_DB_PASSWORD;
var adoPort = process.env.TEST_DB_PORT;
var adoConnectionString = adoUsername+":"+adoPassword+"@tcp(localhost:"+adoPort+")/"+adoDatabaseName+"?tls=false"

Given('I have encrypted_data from {string}', async function (string) {
    var payload = fs.readFileSync(fileDirectory + string, 'utf8');
    encryptedPayload = Buffer.from(payload, 'base64').toString('utf8')
    return 'passed';
});

When('I decrypt the encrypted_data', async function () {
    const config = {
        KMS: 'static',
        Metastore: 'rdbms',
        ServiceName: 'service',
        ProductID: 'product',
        Verbose: false,
        EnableSessionCaching: true,
        ExpireAfter: null,
        CheckInterval: null,
        ConnectionString: adoConnectionString,
        ReplicaReadConsistency: null,
        DynamoDBEndpoint: null,
        DynamoDBRegion: null,
        DynamoDBTableName: null,
        SessionCacheMaxSize: null,
        SessionCacheDuration: null,
        RegionMap: null,
        PreferredRegion: null,
        EnableRegionSuffix: null
    };
    asherah.setup(config);
    decryptedPayload = asherah.decrypt_string('partition', encryptedPayload);
    asherah.shutdown();
    return 'passed';
});

Then('I should get decrypted_data', async function () {
    assert(decryptedPayload != null);
    return 'passed';
});

Then('decrypted_data should be equal to {string}', async function (originalPayload) {
    assert(decryptedPayload == originalPayload);
    return 'passed';
});
