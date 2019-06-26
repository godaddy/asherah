# Asherah

Asherah is an application-layer encryption SDK, currently in incubator status, that provides advanced encryption
features and defense in depth against compromise.

### NOTICE: This is an alpha product

Internally, we are preparing this for production workloads and have a high degree of confidence in it, but we want to
be clear that this should still be considered an incubator project. We have **not** yet had any formal external
security audits of this product. We do not yet consider Asherah as validated for production use. As we receive more
feedback, both internally and externally, APIs and features may be subject to change. Once we have cleared external
audits and hit feature and testing milestones, we will release languages and versions into production status.

Table of Contents
=================

   * [Introduction](#introduction)
   * [Getting Started](#getting-started)
   * [Further Reading](#further-reading)
   * [Supported Languages](#supported-languages)
       * [Feature Support](#feature-support)
   * [Current Status](#current-status)
   * [Contributing](CONTRIBUTING.md)

## Introduction

The Asherah SDK provides advanced encryption techniques exposed via simple APIs for application-layer encryption.
Its goal is to provide an easy-to-use library which abstracts away internal complexity and provides rapid, frequent key rotation
with enterprise scale in mind.

Multiple layers of keys are used in conjunction with a technique known as "envelope encryption". Envelope encryption is a
practice where a key used to encrypt data is itself encrypted by a higher-order key and stored alongside the encrypted data, hence forming an
envelope structure. The master key used at the root of the key hierarchy is typically managed by a Hardware Security Module (HSM)
or Key Management Service (KMS).

The SDK generates cryptographically strong intermediate keys in the hierarchical model and manages their storage via a pluggable
backing datastore. The integration with a HSM or KMS provider for the root (master) key in the hierarchy is implemented using a
similar pluggable model. This allows for supporting a wide variety of datastores and cloud providers for different architectures.

The SDK provides implementations in multiple languages using native interoperability mechanisms to securely manage and
cache internally-generated keys in off-heap protected memory. The combination of secure memory management and the hierarchical
key model's partitioning help minimize attack exposure in the event of compromise. Using the protected memory cache has an added
benefit of reducing interactions with external resources to improve latency and minimize incurred costs.

## Getting Started

The basic use of the SDK proceeds in 3 steps:
 
### Step 1: Create a session factory

A session factory is required to generate encryption/decryption sessions. For simplicity, the session factory uses the
builder pattern, specifically a _step builder_. This ensures all required properties are set before a factory is built.

To obtain an instance of the builder, use the static factory method `newBuilder`. Once you have a builder, you can 
use the `withXXX` setter methods to configure the session factory properties.

Below is an example of a session factory that uses in-memory persistence and static key management.

```java
AppEncryptionSessionFactory appEncryptionSessionFactory = AppEncryptionSessionFactory
    .newBuilder("myservice", "sample_code")
    .withMemoryPersistence() // in-memory metastore persistence only
    .withNeverExpiredCryptoPolicy()
    .withStaticKeyManagementService("secretmasterkey!") // hard-coded/static master key
    .build());
```

### Step 2: Create a session

Use the factory to create a session.

```java
AppEncryption<byte[], byte[]> appEncryptionBytes = appEncryptionSessionFactory.getAppEncryptionBytes("shopper123");
```

The scope of a session is limited to a partition id, i.e. every partition id should have its own session.

### Step 3: Use the session to accomplish the cryptographic task

The SDK supports 2 usage patterns:

#### Encrypt / Decrypt

This usage style is similar to common encryption utilities where payloads are simply encrypted and
decrypted, and it is completely up to the calling application for storage responsibility.

```java
String originalPayloadString = "mysupersecretpayload";

// encrypt the payload 
byte[] dataRowRecordBytes = appEncryptionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

// decrypt the payload 
String decryptedPayloadString = new String(appEncryptionBytes.decrypt(newBytes), StandardCharsets.UTF_8);
```

#### Store / Load

This pattern uses a key-value/document storage model. An `AppEncryption` instance can accept a `Persistence`
implementation and hooks into its load and store calls.

Example `HashMap`-backed `Persistence` implementation:

```java
Persistence dataPersistence = new Persistence<JSONObject>() {

    Map<String, JSONObject> mapPersistence = new HashMap<>();

    @Override 
    public Optional<JSONObject> load(String key) { 
        return Optional.ofNullable(mapPersistence.get(key)); 
    }

    @Override 
    public void store(String key, JSONObject value) { 
        mapPersistence.put(key, value); 
    } 
};
```

Putting it all together, an example end-to-end use of the store and load calls:

```java
// Encrypts the payload, stores it in the dataPersistence and returns a look up key 
String persistenceKey = appEncryptionJson.store(originalPayload.toJsonObject(), dataPersistence);

// Uses the persistenceKey to look-up the payload in the dataPersistence, decrypts the payload if any and then returns it 
Optional<JSONObject> payload = appEncryptionJson.load(persistenceKey, dataPersistence);
```

## Further Reading

* [Design And Architecture](docs/DesignAndArchitecture.md)
* [System Requirements](docs/SystemRequirements.md)
* [Key Management Service](docs/KeyManagementService.md)
* [Metastore Persistence](docs/Metastore.md)
* [Key Caching](docs/KeyCaching.md)
* [Common APIs and Algorithm Internals](docs/Internals.md)
* [Roadmap](docs/ROADMAP.md)
* [Testing Approach](docs/TestingApproach.md)
* [FAQ](docs/FAQ.md)

## Supported Languages

* [Java](languages/java/app-encryption)
* [.NET](languages/csharp/AppEncryption)
* Go (coming soon!)

### Feature Support

| Feature            | Java | .NET |
| ------------------ | ---- | ---- |
| AWS KMS Support    | Yes  | Yes  |
| RDBMS Metastore    | Yes  | Yes  |
| DynamoDB Metastore | Yes  | Yes  |


## Current Status

Asherah is currently in incubator status. Please refer to our [Roadmap](docs/ROADMAP.md) for additional information.
