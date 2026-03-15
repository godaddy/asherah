# SessionFactory Upgrade Guide

This guide provides step-by-step instructions for upgrading from the legacy `SessionFactory` (and `IMetastore<JObject>`-based stack) to the new `SessionFactory` in the Core namespace that uses `IKeyMetastore`.

## Table of Contents

- [Overview](#overview)
- [Key Changes](#key-changes)
- [Migration Steps](#migration-steps)
- [Complete Migration Example](#complete-migration-example)
- [Session API Changes](#session-api-changes)
- [Additional Notes](#additional-notes)

## Overview

The new `SessionFactory` lives in the `GoDaddy.Asherah.AppEncryption.Core` namespace and is built around `IKeyMetastore` instead of `IMetastore<JObject>`. The key metastore interface uses key records and async operations, and the new session factory exposes only byte-based encryption sessions (`IEncryptionSession`). There is no direct compatibility between the old and new APIs; upgrading requires switching metastore, builder, and session usage together.

Applications are expected to use **one** of the two session factories (either the legacy or the Core). Using both in the same application is not recommended.

## Key Changes

| Area | Old (Legacy) | New (Core) |
|------|--------------|------------|
| **Namespace** | `GoDaddy.Asherah.AppEncryption` | `GoDaddy.Asherah.AppEncryption.Core` |
| **Metastore** | `IMetastore<JObject>` (sync, JSON values) | `IKeyMetastore` (async, `IKeyRecord` values) |
| **Builder** | `WithMetastore()`, `WithInMemoryMetastore()` | `WithKeyMetastore()` (required) |
| **Logger** | Optional via `WithLogger()` | Required via `WithLogger()` |
| **Session types** | `GetSessionBytes()`, `GetSessionJson()`, `GetSessionJsonAsJson()`, `GetSessionBytesAsJson()` | `GetSession(partitionId)` only |
| **Session return** | `Session<TP, TD>` (generic) | `IEncryptionSession` (byte[] encrypt/decrypt only) |
| **Metrics** | `WithMetrics(IMetrics)` on builder | Not available on new builder |

## Migration Steps

### Step 1: Use the Core namespace and builder

**Old:**
```c#
using GoDaddy.Asherah.AppEncryption;

SessionFactory sessionFactory = SessionFactory.NewBuilder("product", "service")
    .WithMetastore(metastore)  // IMetastore<JObject>
    .WithCryptoPolicy(cryptoPolicy)
    .WithKeyManagementService(keyManagementService)
    .Build();
```

**New:**
```c#
using GoDaddy.Asherah.AppEncryption.Core;

SessionFactory sessionFactory = SessionFactory.NewBuilder("product", "service")
    .WithKeyMetastore(keyMetastore)  // IKeyMetastore
    .WithCryptoPolicy(cryptoPolicy)
    .WithKeyManagementService(keyManagementService)
    .WithLogger(logger)  // Required
    .Build();
```

### Step 2: Replace the metastore with an IKeyMetastore implementation

The legacy factory uses `IMetastore<JObject>` (e.g. `DynamoDbMetastoreImpl`, `AdoMetastoreImpl`, `InMemoryMetastoreImpl<JObject>`). The new factory requires `IKeyMetastore`.

- **DynamoDB:** Use the new plugin `GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore.DynamoDbMetastore` (see [Plugins Upgrade Guide – KeyMetastore](plugins-upgrade-guide.md#upgrading-to-the-new-keymetastore-plugin)).
- **In-memory (e.g. tests):** Use `GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore.InMemoryKeyMetastore`.

There is no adapter from `IMetastore<JObject>` to `IKeyMetastore`; the interfaces differ (sync vs async, `JObject` vs `IKeyRecord`). You must use an `IKeyMetastore` implementation.

### Step 3: Provide a logger

The new builder requires a logger. Pass `ILogger` from your logging abstraction (e.g. `Microsoft.Extensions.Logging`) via `WithLogger(logger)`.

### Step 4: Replace session creation and usage

**Old (bytes-only usage):**
```c#
Session<byte[], byte[]> session = sessionFactory.GetSessionBytes("partition_id");
byte[] encrypted = session.Encrypt(payload);
byte[] decrypted = session.Decrypt(encrypted);
```

**New:**
```c#
using (IEncryptionSession session = sessionFactory.GetSession("partition_id"))
{
    byte[] encrypted = session.Encrypt(payload);
    byte[] decrypted = session.Decrypt(encrypted);
}
```

The new API does not offer `GetSessionJson`, `GetSessionJsonAsJson`, or `GetSessionBytesAsJson`. If you need JSON payloads, serialize to/from byte[] (e.g. UTF-8) and use `GetSession` + `Encrypt`/`Decrypt`.

**Old (JSON session):**
```c#
Session<JObject, byte[]> session = sessionFactory.GetSessionJson("partition_id");
byte[] encrypted = session.Encrypt(jObjectPayload);
```

**New (equivalent):**
```c#
// Serialize to bytes, then encrypt
byte[] payloadBytes = Encoding.UTF8.GetBytes(jObjectPayload.ToString());
using (IEncryptionSession session = sessionFactory.GetSession("partition_id"))
{
    byte[] encrypted = session.Encrypt(payloadBytes);
}
```

### Step 5: Remove or replace metrics usage

The Core `SessionFactory` builder has no `WithMetrics()` method. Remove metrics configuration from the factory build, or handle metrics elsewhere (e.g. application-level or wrapper).

## Complete Migration Example

**Before (legacy):**
```c#
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.AppEncryption.Persistence;

IMetastore<JObject> metastore = DynamoDbMetastoreImpl.NewBuilder("us-west-2")
    .Build();

SessionFactory sessionFactory = SessionFactory.NewBuilder("my_product", "my_service")
    .WithMetastore(metastore)
    .WithCryptoPolicy(new NeverExpiredCryptoPolicy())
    .WithKeyManagementService(keyManagementService)
    .WithLogger(logger)
    .Build();

Session<byte[], byte[]> session = sessionFactory.GetSessionBytes("user_123");
byte[] drr = session.Encrypt(plaintext);
byte[] decrypted = session.Decrypt(drr);
session.Dispose();
sessionFactory.Dispose();
```

**After (Core):**
```c#
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore;

IKeyMetastore keyMetastore = DynamoDbMetastore.NewBuilder()
    .WithDynamoDbClient(amazonDynamoDbClient)
    .WithOptions(new DynamoDbMetastoreOptions { KeyRecordTableName = "EncryptionKey" })
    .Build();

SessionFactory sessionFactory = SessionFactory.NewBuilder("my_product", "my_service")
    .WithKeyMetastore(keyMetastore)
    .WithCryptoPolicy(new NeverExpiredCryptoPolicy())
    .WithKeyManagementService(keyManagementService)
    .WithLogger(logger)
    .Build();

using (IEncryptionSession session = sessionFactory.GetSession("user_123"))
{
    byte[] drr = session.Encrypt(plaintext);
    byte[] decrypted = session.Decrypt(drr);
}
sessionFactory.Dispose();
```

## Session API Changes

| Legacy `Session<byte[], byte[]>` | New `IEncryptionSession` |
|----------------------------------|---------------------------|
| `Encrypt(byte[] payload)` | `Encrypt(byte[] payload)` |
| `Decrypt(byte[] dataRowRecord)` | `Decrypt(byte[] dataRowRecord)` |
| — | `EncryptAsync(byte[] payload)` |
| — | `DecryptAsync(byte[] dataRowRecord)` |
| `IDisposable` | `IDisposable` |

The new session is byte-only; JSON-oriented workflows must be handled by serializing/deserializing outside the session.

## Additional Notes

- **One factory per application:** Use either the legacy or the Core SessionFactory in an application, not both.
- **In-memory testing:** Use `InMemoryKeyMetastore` from the `GoDaddy.Asherah.AppEncryption.PlugIns.Testing` package when building the Core `SessionFactory` in tests.
- **KeyMetastore plugins:** Migrating to the new KeyMetastore plugin (e.g. DynamoDB) implies using the new SessionFactory, since those plugins implement `IKeyMetastore`, which only the Core factory accepts. See the [Plugins Upgrade Guide – Upgrading to the new KeyMetastore Plugin](plugins-upgrade-guide.md#upgrading-to-the-new-keymetastore-plugin).
- **Dispose:** Both the session and the session factory should be disposed when no longer needed; prefer `using` for sessions.
