# Asherah - C#
Application level envelope encryption SDK for C# with support for cloud-agnostic data storage and key management.

  * [Quick Start](#quick-start)
  * [How to Use Asherah](#how-to-use-asherah)
    * [Define the Metastore](#define-the-metastore)
    * [Define the Key Management Service](#define-the-key-management-service)
    * [Define the Crypto Policy](#define-the-crypto-policy)
    * [(Optional) Enable Metrics](#optional-enable-metrics)
    * [Build a Session Factory](#build-a-session-factory)
    * [Performing Cryptographic Operations](#performing-cryptographic-operations)
  * [Deployment Notes](#deployment-notes)
    * [Handling read\-only Docker containers](#handling-read-only-docker-containers)
  * [Development Notes](#development-notes)

## Quick Start

```c#
// Create a session factory. The builder steps used below are for testing only.
using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
    .NewBuilder("some_product", "some_service")
    .WithMemoryPersistence()
    .WithNeverExpiredCryptoPolicy()
    .WithStaticKeyManagementService("secretmasterkey!")
    .Build())
{
    // Now create a cryptographic session for a partition.
    using (AppEncryption<byte[], byte[]> appEncryptionBytes =
        appEncryptionSessionFactory.GetAppEncryptionBytes("some_partition"))
    {
        // Encrypt some data
        const string originalPayloadString = "mysupersecretpayload";
        byte[] dataRowRecordBytes = appEncryptionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

        // Decrypt the data
        string decryptedPayloadString = Encoding.UTF8.GetString(appEncryptionBytes.Decrypt(dataRowRecordBytes));
    }
}
```

A more extensive example is the [Reference Application](../../../samples/csharp/ReferenceApp/), which will evolve along 
with the SDK.

## How to Use Asherah

Before you can start encrypting data, you need to define Asherah's required pluggable components. Below we show how to
build the various options for each component.

### Define the  Metastore

Detailed information about the Metastore, including any provisioning steps, can be found [here](../../../docs/Metastore.md).

#### ADO Metastore

TODO : Do we need to add something here, like we did for Java?

```c#
// Create / retrieve a DbProviderFactory for your target vendor, as well as the connection string
DbProviderFactory dbProviderFactory = ...;
string connectionString = ...;

// Build the ADO Metastore
IMetastorePersistence<JObject> adoMetastorePersistence = AdoMetastorePersistenceImpl
    .NewBuilder(dbProviderFactory, connectionString).Build();
}
```

#### DynamoDB Metastore

```c#
// Setup region via global default or via other AWS .NET SDK mechanisms
AWSConfigs.AWSRegion = "us-west-2";

// Build the DynamoDB Metastore.
IMetastorePersistence<JObject> dynamoDbMetastorePersistence = DynamoDbMetastorePersistenceImpl.NewBuilder().Build();
}
```

#### In-memory Metastore (FOR TESTING ONLY)

```c#
MetastorePersistence<JObject> metastorePersistence = new MemoryPersistenceImpl<>();
```

### Define the Key Management Service
Detailed information about the Key Management Service can be found [here](../../../docs/KeyManagementService.md).

#### AWS KMS

```c#
// Create a dictionary of region and arn that will all be used when creating a System Key
Dictionary<string, string> regionDictionary = new Dictionary<string, string>
{
    { "us-east-1", "arn_of_us-east-1" },
    { "us-east-2", "arn_of_us-east-2" },
    ...
};

// Build the Key Management Service using the region dictionary and your preferred (usually current) region
AWSKeyManagementServiceImpl keyManagementService = AWSKeyManagementServiceImpl.newBuilder(regionDictionary, "us-east-1").build();
```

#### Static KMS (FOR TESTING ONLY)

```c#
KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl("secretmasterkey!");
```

### Define the Crypto Policy
Detailed information on Crypto Policy can be found [here](../../../docs/CryptoPolicy.md). The Crypto Policy's effect 
on key caching is explained [here](../../../docs/KeyCaching.md).

#### Basic Expiring Crypto Policy

```c#
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
    .NewBuilder()
    .WithKeyExpirationDays(90)
    .WithRevokeCheckMinutes(60)
    .Build();
```

#### Never Expired Crypto Policy (FOR TESTING ONLY)

```c#
CryptoPolicy neverExpiredCryptoPolicy = new NeverExpiredCryptoPolicy();
```

### (Optional) Enable Metrics
The library uses [App.Metrics](https://www.app-metrics.io/) for metrics, which are disabled by default.
If metrics are left disabled, we simply create and use an `IMetrics`instance whose 
[Enabled flag](https://www.app-metrics.io/getting-started/fundamentals/configuration/) is disabled.

To enable metrics generation, simply pass in an existing `IMetrics` instance to the final optional builder step when 
creation an `AppEncryptionSessionFactory`.

### Build a Session Factory

A session factory can now be built using the components we defined above.

```c#
AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.NewBuilder("productId", "systemId")
     .WithMetastorePersistence(metastorePersistence)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .WithMetrics(metrics) // Optional
     .Build();
```

**NOTE:** We recommend that every service have its own session factory, preferably as a singleton instance within the service.
This will allow you to leverage caching and minimize resource usage. Always remember to close the session factory before exiting
the service to ensure that all resources held by the factory, including the cache, are disposed of properly.

### Performing Cryptographic Operations

Create an `AppEncryption` session to be used for cryptographic operations.

```c#
AppEncryption<byte[], byte[]> appEncryptionBytes = appEncryptionSessionFactory.GetAppEncryptionBytes("some_user");
```

The different usage styles are explained below.

**NOTE:** Remember to close the `AppEncryption` session after all cryptographic operations to dispose of associated resources.

#### Plain Encrypt/Decrypt Style
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is 
completely up to the calling application for storage responsibility.

```c#
string originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = appEncryptionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

// decrypt the payload
string decryptedPayloadString = Encoding.UTF8.GetString(appEncryptionBytes.Decrypt(newDataRowRecordBytes));
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
string persistenceKey = appEncryptionJsonImpl.Store(originalPayload.ToJObject(), dictionaryPersistence);

// Uses the persistenceKey to look-up the payload in the dictionaryPersistence, decrypts the payload if any and then returns it
Option<JObject> payload = appEncryptionJsonImpl.Load(persistenceKey, dictionaryPersistence);
```



## Deployment Notes

### Handling read-only Docker containers

Dotnet enables debugging and profiling by default causing some system level writes. Disabling them ensures that the 
SDK can be used in a read-only environment.

To do so, simply set the environment variable `COMPlus_EnableDiagnostics` to 0

```dockerfile
ENV COMPlus_EnableDiagnostics=0
```

Our [sample application's](../../../samples/csharp/ReferenceApp/images/runtime/Dockerfile) Dockerfile can be used for 
reference:

## Development Notes

Some unit tests will use the AWS SDK, If you don’t already have a local
[AWS credentials file](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html),
create a *dummy* file called **`~/.aws/credentials`** with the below contents:

```
[default]
aws_access_key_id = foobar
aws_secret_access_key = barfoo
```

Alternately, you can set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

