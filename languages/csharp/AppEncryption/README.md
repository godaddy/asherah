# AppEncryptionCSharp
Application level encryption C#

Table of Contents
=================

  * [Basic Example](#basic-example)
  * [Library Details](#library-details)
    * [Usage Styles](#usage-styles)
      * [Custom Persistence via Load/Store methods](#custom-persistence-via-loadstore-methods)
      * [Plain Encrypt/Decrypt style](#plain-encryptdecrypt-style)
    * [Metastore](#metastore)
      * [ADO Metastore](#ado-metastore)
      * [DynamoDB Metastore](#dynamodb-metastore)
    * [Key Management Service](#key-management-service)
      * [AWS KMS](#aws-kms)
    * [Crypto Policy](#crypto-policy)
    * [Key Caching](#key-caching)
    * [Metrics](#metrics)
  * [Deployment Notes](#deployment-notes)
  * [Library Development Notes](#library-development-notes)
    * [Running Tests Locally via Docker Image](#running-tests-locally-via-docker-image)

## Basic Example

The App Encryption library generally uses the **builder pattern** to define objects.

```c#
// First build a basic Crypto Policy that expires
// keys after 90 days and has a cache TTL of 60 minutes
CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
    .NewBuilder()
    .WithKeyExpirationDays(90)
    .WithRevokeCheckMinutes(60)
    .Build();

// Create a session factory for this app. Normally this would be done upon app startup and the
// same factory would be used anytime a new session is needed for a partition (e.g., shopper).
// We've split it out into multiple using blocks to underscore this point.
using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
    .NewBuilder("productId", "systemId")
    .WithMemoryPersistence() // in-memory metastore persistence only
    .WithCryptoPolicy(cryptoPolicy)
    .WithStaticKeyManagementService("secretmasterkey!") // hard-coded/static master key
    .Build())
{
    // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
    // for a transaction and is disposed automatically after use due to the IDisposable implementation.
    using (AppEncryption<byte[], byte[]> appEncryptionBytes =
        appEncryptionSessionFactory.GetAppEncryptionBytes("shopper123"))
    {
        // Now encrypt some data
        const string originalPayloadString = "mysupersecretpayload";

        byte[] dataRowRecordBytes = appEncryptionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

        // Consider this us "persisting" the DRR
        string dataRowString = Convert.ToBase64String(dataRowRecordBytes);
        logger.LogInformation("dataRowRecord as string = {dataRow}", dataRowString);

        byte[] newDataRowRecordBytes = Convert.FromBase64String(dataRowString);

        // Ensure we can decrypt the data, too
        string decryptedPayloadString = Encoding.UTF8.GetString(appEncryptionBytes.Decrypt(newDataRowRecordBytes));

        logger.LogInformation("decryptedPayloadString = {payload}", decryptedPayloadString);
        logger.LogInformation("matches = {result}", originalPayloadString.Equals(decryptedPayloadString));
    }
}
```
You can also review the [Reference Application](../../../samples/csharp/ReferenceApp/), which will evolve along with the library and show more detailed usage.

## Library Details

### Usage Styles

#### Custom Persistence via Load/Store methods
The App Encryption library supports a key-value/document storage model. An [AppEncryption](AppEncryption/AppEncryption.cs) instance can accept a [Persistence](AppEncryption/Persistence/Persistence.cs) implementation
and hooks into its `Load` and `Store` calls. This can be seen in the abstract class definition:

 ```c#
public abstract class AppEncryption<TP, TD> : IDisposable
{

    /// <summary>
    /// Uses a persistence key to load a Data Row Record from the provided data persistence store, if any,
    /// and returns the decrypted payload.
    /// </summary>
    /// <param name="persistenceKey">Key used to retrieve the Data Row Record</param>
    /// <param name="dataPersistence">The persistence store from which to retrieve the DRR</param>
    /// <returns>The decrypted payload, if found in persistence</returns>
    public virtual Option<TP> Load(string persistenceKey, Persistence<TD> dataPersistence)
    {
        return dataPersistence.Load(persistenceKey).Map(Decrypt);
    }

    /// <summary>
    /// Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store, and
    /// returns its associated persistence key for future lookups.
    /// </summary>
    /// <param name="payload">Payload to be encrypted</param>
    /// <param name="dataPersistence">The persistence store where the encrypted DRR should be stored</param>
    /// <returns>The persistence key associated with the stored Data Row Record</returns>
    public virtual string Store(TP payload, Persistence<TD> dataPersistence)
    {
        TD dataRowRecord = Encrypt(payload);
        return dataPersistence.Store(dataRowRecord);
    }

    /// <summary>
    /// Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store with
    /// given key
    /// </summary>
    /// <param name="key">Key against which the encrypted DRR will be saved</param>
    /// <param name="payload">Payload to be encrypted</param>
    /// <param name="dataPersistence">The persistence store where the encrypted DRR should be stored</param>
    public virtual void Store(string key, TP payload, Persistence<TD> dataPersistence)
    {
        TD dataRowRecord = Encrypt(payload);
        dataPersistence.Store(key, dataRowRecord);
    }
```

Example `Dictionary`-backed `Persistence` implementation:

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

Putting it all together, an example end-to-end use of the store and load calls:

```c#
// Encrypts the payload, stores it in the dictionaryPersistence and returns a look up key
string persistenceKey = appEncryptionJsonImpl.Store(originalPayload.ToJObject(), dictionaryPersistence);

// Uses the persistenceKey to look-up the payload in the dictionaryPersistence, decrypts the payload if any and then returns it
Option<JObject> payload = appEncryptionJsonImpl.Load(persistenceKey, dictionaryPersistence);
```

#### Plain Encrypt/Decrypt Style
This usage style is similar to common encryption utilities where payloads are simply encrypted and decrypted, and it is completely up to the calling application for storage responsibility.

```c#
string originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = appEncryptionBytes.Encrypt(Encoding.UTF8.GetBytes(originalPayloadString));

// decrypt the payload
string decryptedPayloadString = Encoding.UTF8.GetString(appEncryptionBytes.Decrypt(newDataRowRecordBytes));
```


### Metastore
Please refer to the [Java lib's notes](../../java/app-encryption#metastore) until documentation cleanup/refactor is complete. It contains deployment info, data size estimates, etc. Until then, we will simply provide code usage examples here.

#### ADO Metastore

```c#
// Create / retrieve a DbProviderFactory for your target vendor, as well as the connection string
DbProviderFactory dbProviderFactory = ...;
string connectionString = ...;

// Build the ADO Metastore
IMetastorePersistence<JObject> adoMetastorePersistence = AdoMetastorePersistenceImpl
    .NewBuilder(dbProviderFactory, connectionString).Build();

// Use the Metastore for the session factory
using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.NewBuilder("productId", "systemId")
     .WithMetastorePersistence(adoMetastorePersistence)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .Build()) {

    // ...
}
```

#### DynamoDB Metastore

```c#
// Setup region via global default or via other AWS .NET SDK mechanisms
AWSConfigs.AWSRegion = "us-west-2";

// Build the DynamoDB Metastore.
IMetastorePersistence<JObject> dynamoDbMetastorePersistence = DynamoDbMetastorePersistenceImpl.NewBuilder().Build();

// Use the Metastore for the session factory
using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.NewBuilder("productId", "systemId")
     .WithMetastorePersistence(dynamoDbMetastorePersistence)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .Build()) {

    // ...
}
```

### Key Management Service
Please refer to the [Java lib's notes](../../java/app-encryption#key-management-service) until documentation cleanup/refactor is complete. Until then, we will simply provide code usage examples here.

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

// Provide the above keyManagementService to the session factory builder
using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.NewBuilder("productId", "systemId")
     .WithMetastorePersistence(metastorePersistence)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .Build()) {

    // ...
}
```

### Crypto Policy
Please refer to the [Java lib's notes](../../java/app-encryption#crypto-policy) until documentation cleanup/refactor is complete. The only difference code-wise is the convention of using PascalCase instead of camelCase for method naming.

### Key Caching
Please refer to the [Java lib's notes](../../java/app-encryption#key-caching) until documentation cleanup/refactor is complete. The only difference code-wise is the directory path to the C# classes, but semantics should be the same.

### Metrics
The library uses [App.Metrics](https://www.app-metrics.io/) for metrics, which are disabled by default. If metrics are left disabled, we simply create and use an `IMetrics`instance whose [Enabled flag](https://www.app-metrics.io/getting-started/fundamentals/configuration/) is disabled.

To enable metrics generation, simply pass in an existing `IMetrics` instance to the final optional builder step when creation an `AppEncryptionSessionFactory`:

```c#
IMetrics metrics = ...;

using (AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory.NewBuilder("productId", "systemId")
     .WithMetastorePersistence(metastorePersistence)
     .WithCryptoPolicy(policy)
     .WithKeyManagementService(keyManagementService)
     .WithMetrics(metrics)
     .Build()) {

    // ...
}
```
To report metrics to AmazonCloudWatch please refer to AppMetricsCloudWatchReporter (add link when we open source it).

## Deployment Notes
Please refer to the [Java lib's notes](../../java/app-encryption#deployment-notes) until documentation cleanup/refactor is complete.

## Library Development Notes

### Running Tests Locally via Docker Image
Below is an example of how to run tests locally using a Docker image. This one is using the build image used for Jenkins build/deployment, but could be replaced with other images for different targeted platforms. Note this is run from your project directory.

```console
[user@machine AppEncryptionCSharp]$ docker build images/build/
...
Successfully built <generated_image_id>
[user@machine AppEncryptionCSharp]$ docker run -it --rm -v $HOME/.nuget:/home/jenkins/.nuget -v "$PWD":/usr/app/src -w /usr/app/src --ulimit memlock=-1:-1 --ulimit core=-1:-1 <generated_image_id> dotnet clean -c Release && dotnet restore && dotnet build -c Release --no-restore && dotnet test -c Release --no-build /p:CollectCoverage=true /p:Exclude=\"[xunit*]*,[*.IntegrationTests]*,[*.Tests]*\" /p:CoverletOutputFormat=cobertura /p:ExcludeByFile="../**/MacOS/**/*.cs" && dotnet pack -c Release --no-restore
...
```
*Note*: The above build is known to work on macOS due to how the bind mounts map UIDs. On Linux systems you will likely need to add the optional build arguments:

``` console
[user@machine AppEncryptionCSharp]$ docker build --build-arg UID=$(id -u) --build-arg GID=$(id -g) images/build
```

This will create the container's user with your UID so that it has full access to the .nuget directory.
