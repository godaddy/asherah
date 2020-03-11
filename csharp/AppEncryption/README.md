# Asherah - C#
Application level envelope encryption SDK for C# with support for cloud-agnostic data storage and key management.

  * [Quick Start](#quick-start)
  * [How to Use Asherah](#how-to-use-asherah)
    * [Define the Metastore](#define-the-metastore)
    * [Define the Key Management Service](#define-the-key-management-service)
    * [Define the Crypto Policy](#define-the-crypto-policy)
      * [(Optional) Enable Session Caching](#optional-enable-session-caching)
    * [(Optional) Enable Metrics](#optional-enable-metrics)
    * [Build a Session Factory](#build-a-session-factory)
    * [Performing Cryptographic Operations](#performing-cryptographic-operations)
  * [Deployment Notes](#deployment-notes)
    * [Handling read\-only Docker containers](#handling-read-only-docker-containers)
  * [Development Notes](#development-notes)

## Quick Start

```c#
// Create a session factory. The builder steps used below are for testing only.
using (SessionFactory sessionFactory = SessionFactory
    .NewBuilder("some_product", "some_service")
    .WithMemoryPersistence()
    .WithNeverExpiredCryptoPolicy()
    .WithStaticKeyManagementService("secretmasterkey!")
    .Build())
{
    // Now create a cryptographic session for a partition.
    using (Session<byte[], byte[]> sessionBytes =
        sessionFactory.GetSessionBytes("some_partition"))
    {
        // Encrypt some data
        const string originalPayloadString = "mysupersecretpayload";
        byte[] dataRowRecordBytes = sessionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

        // Decrypt the data
        string decryptedPayloadString = Encoding.UTF8.GetString(sessionBytes.Decrypt(dataRowRecordBytes));
    }
}
```

A more extensive example is the [Reference Application](../../samples/csharp/ReferenceApp/), which will evolve along 
with the SDK.

## How to Use Asherah

Before you can start encrypting data, you need to define Asherah's required pluggable components. Below we show how to
build the various options for each component.

### Define the Metastore

Detailed information about the Metastore, including any provisioning steps, can be found [here](../../docs/Metastore.md).

#### RDBMS Metastore

Asherah can connect to a relational database by accepting an ADO DbProviderFactory and a connection string.

```c#
// Create / retrieve a DbProviderFactory for your target vendor, as well as the connection string
DbProviderFactory dbProviderFactory = ...;
string connectionString = ...;

// Build the ADO Metastore
IMetastore<JObject> adoMetastore = AdoMetastoreImpl.NewBuilder(dbProviderFactory, connectionString).Build();
```

#### DynamoDB Metastore

```c#
// Setup region via global default or via other AWS .NET SDK mechanisms
AWSConfigs.AWSRegion = "us-west-2";

// Build the DynamoDB Metastore.
IMetastore<JObject> dynamoDbMetastore = DynamoDbMetastoreImpl.NewBuilder().Build();
```

#### In-memory Metastore (FOR TESTING ONLY)

```c#
IMetastore<JObject> metastore = new InMemoryPersistenceImpl<JObject>();
```

### Define the Key Management Service
Detailed information about the Key Management Service can be found [here](../../docs/KeyManagementService.md).

#### AWS KMS

```c#
// Create a dictionary of region and ARN pairs that will all be used when creating a System Key
Dictionary<string, string> regionDictionary = new Dictionary<string, string>
{
    { "us-east-1", "arn_of_us-east-1" },
    { "us-east-2", "arn_of_us-east-2" },
    ...
};

// Build the Key Management Service using the region dictionary and your preferred (usually current) region
KeyManagementService keyManagementService = AwsKeyManagementServiceImpl.newBuilder(regionDictionary, "us-east-1").Build();
```

#### Static KMS (FOR TESTING ONLY)

```c#
KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl("secretmasterkey!");
```

### Define the Crypto Policy
Detailed information on Crypto Policy can be found [here](../../docs/CryptoPolicy.md). The Crypto Policy's effect 
on key caching is explained [here](../../docs/KeyCaching.md).

#### Basic Expiring Crypto Policy

```c#
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
    .WithKeyExpirationDays(90)
    .WithRevokeCheckMinutes(60)
    .Build();
```

#### (Optional) Enable Session Caching

Session caching is disabled by default. Enabling it is primarily useful if you are working with stateless workloads and the 
shared session can't be used by the calling app.

To enable session caching, simply use the optional builder step `WithCanCacheSessions(true)` when building a crypto policy.

```c#
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
    .WithKeyExpirationDays(90)
    .WithRevokeCheckMinutes(60)
    .WithCanCacheSessions(true)    // Enable session cache
    .WithSessionCacheMaxSize(200)    // Define the number of maximum sessions to cache
    .WithSessionCacheExpireMillis(5000)    // Evict the session from cache after some milliseconds
    .Build();
```

#### Never Expired Crypto Policy (FOR TESTING ONLY)

```c#
CryptoPolicy neverExpiredCryptoPolicy = new NeverExpiredCryptoPolicy();
```

### (Optional) Enable Metrics
Asherah's C# implementation uses [App.Metrics](https://www.app-metrics.io/) for metrics, which are disabled by default.
If metrics are left disabled, we simply create and use an `IMetrics`instance whose 
[Enabled flag](https://www.app-metrics.io/getting-started/fundamentals/configuration/) is disabled.

To enable metrics generation, simply pass in an existing `IMetrics`
instance to the final optional builder step when creating a
`SessionFactory`.

The following metrics are available:
- *ael.drr.decrypt:* Total time spent on all operations that were needed to decrypt.
- *ael.drr.encrypt:* Total time spent on all operations that were needed to encrypt.
- *ael.kms.aws.decrypt.\<region\>:* Time spent on decrypting the region-specific keys.
- *ael.kms.aws.decryptkey:* Total time spend in decrypting the key which would include the region-specific decrypt calls
in case of transient failures.
- *ael.kms.aws.encrypt.\<region\>:* Time spent on data key plain text encryption for each region.
- *ael.kms.aws.encryptkey:* Total time spent in encrypting the key which would include the region-specific generatedDataKey
and parallel encrypt calls.
- *ael.kms.aws.generatedatakey.\<region\>:* Time spent to generate the first data key which is then encrypted in remaining regions.
- *ael.metastore.ado.load:* Time spent to load a record from ado metastore.
- *ael.metastore.ado.loadlatest:* Time spent to get the latest record from ado metastore.
- *ael.metastore.ado.store:* Time spent to store a record into ado metastore.
- *ael.metastore.dynamodb.load:* Time spent to load a record from DynamoDB metastore.
- *ael.metastore.dynamodb.loadlatest:* Time spent to get the latest record from DynamoDB metastore.
- *ael.metastore.dynamodb.store:* Time spent to store a record into DynamoDB metastore.

### Build a Session Factory

A session factory can now be built using the components we defined above.

```c#
SessionFactory sessionFactory = SessionFactory.NewBuilder("some_product", "some_service")
     .WithMetastore(metastore)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .WithMetrics(metrics) // Optional
     .Build();
```

**NOTE:** We recommend that every service have its own session factory, preferably as a singleton instance within the service.
This will allow you to leverage caching and minimize resource usage. Always remember to close the session factory before exiting
the service to ensure that all resources held by the factory, including the cache, are disposed of properly.

### Performing Cryptographic Operations

Create a `Session` session to be used for cryptographic operations.

```c#
Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes("some_user");
```

The different usage styles are explained below.

**NOTE:** Remember to close the session after all cryptographic operations to dispose of associated resources.

#### Plain Encrypt/Decrypt Style
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is 
completely up to the calling application for storage responsibility.

```c#
string originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = sessionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

// decrypt the payload
string decryptedPayloadString = Encoding.UTF8.GetString(dataRowRecordBytes.Decrypt(newDataRowRecordBytes));
```

#### Custom Persistence via Store/Load methods
Asherah supports a key-value/document storage model. An [AppEncryption](AppEncryption/AppEncryption.cs) instance can 
accept a [Persistence](AppEncryption/Persistence/Persistence.cs) implementation and hook into its `Load` and `Store` 
calls.

An example `Dictionary`-backed `Persistence` implementation:

```c#
public class DictionaryPersistence : Persistence<JObject>
{
    Dictionary<string, JObject> dictionaryPersistence = new Dictionary<string, JObject>();

    public override Option<JObject> Load(String key)
    {
        return dictionaryPersistence.TryGetValue(key, out JObject result) ? result : Option<JObject>.None;
    }

    public override void Store(String key, JObject value)
    {
        dictionaryPersistence.Add(key, value);
    }
}
```

An example end-to-end use of the store and load calls:

```c#
// Encrypts the payload, stores it in the dictionaryPersistence and returns a look up key
string persistenceKey = sessionJson.Store(originalPayload.ToJObject(), dictionaryPersistence);

// Uses the persistenceKey to look-up the payload in the dictionaryPersistence, decrypts the payload if any and then returns it
Option<JObject> payload = sessionJson.Load(persistenceKey, dictionaryPersistence);
```

## Deployment Notes

### Handling read-only Docker containers

Dotnet enables debugging and profiling by default causing filesystem writes. Disabling them ensures that the 
SDK can be used in a read-only container.

To do so, simply set the environment variable `COMPlus_EnableDiagnostics` to 0

```dockerfile
ENV COMPlus_EnableDiagnostics=0
```

Our [sample application's](../../samples/csharp/ReferenceApp/images/runtime/Dockerfile) Dockerfile can be used for 
reference.

## Development Notes

### Unit Tests

Some unit tests will use the AWS SDK, If you don’t already have a local
[AWS credentials file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html),
create a *dummy* file called **`~/.aws/credentials`** with the below contents:

```
[default]
aws_access_key_id = foobar
aws_secret_access_key = barfoo
```

Alternately, you can set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

### Regression Tests

The regression test use configuration parameters that can be passed either by using a `config.yaml` file or by
setting the environment variables. The below table outlines the parameter names and their default values (if any)

| Config File                  	| Environment Variable            	| Default Value 	|
|------------------------------	|---------------------------------	|---------------	|
| kmsType                      	| KMS_TYPE                        	| static        	|
| kmsAwsRegionArnTuples        	| KMS_AWS_REGION_ARN_TUPLES       	| N/A           	|
| kmsAwsPreferredRegion        	| KMS_AWS_PREFERRED_REGION        	| us-west-2     	|
| metastoreType                	| METASTORE_TYPE                  	| memory        	|
| metastoreAdoConnectionString 	| METASTORE_ADO_CONNECTION_STRING 	| N/A           	|
