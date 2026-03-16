# Plugins Upgrade Guide

This guide provides step-by-step instructions for upgrading from obsolete plugin implementations to the new recommended plugins.

## Table of Contents

- [Upgrading to the new KeyManagementService Plugin](#upgrading-to-the-new-keymanagementservice-plugin)
- [Upgrading to the new KeyMetastore Plugin](#upgrading-to-the-new-keymetastore-plugin)

## Upgrading to the new KeyManagementService Plugin

The `AwsKeyManagementServiceImpl` class is obsolete and will be removed in a future version. This guide will help you migrate to the new `KeyManagementService` plugin.

### Key Changes

1. **Namespace**: Changed from `GoDaddy.Asherah.AppEncryption.Kms` to `GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms`
2. **Logger**: Now requires `ILoggerFactory` instead of `ILogger`
3. **Region/ARN Configuration**: Dictionary-based approach replaced with builder pattern or configuration-based options
4. **Preferred Region**: No longer a constructor parameter; handled via region ordering or `OptimizeByRegions()` method

### Migration Steps

#### Step 1: Update Namespace

**Old:**
```c#
using GoDaddy.Asherah.AppEncryption.Kms;
```

**New:**
```c#
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
```

#### Step 2: Convert Dictionary to New Format

**Old Approach:**
```c#
Dictionary<string, string> regionDictionary = new Dictionary<string, string>
{
    { "us-east-1", "arn_of_us-east-1" },
    { "us-east-2", "arn_of_us-east-2" },
    { "us-west-2", "arn_of_us-west-2" }
};

KeyManagementService keyManagementService = AwsKeyManagementServiceImpl.NewBuilder(regionDictionary, "us-east-1")
    .WithCredentials(myAwsCredentials)
    .WithLogger(myLogger)
    .Build();
```

**New Approach - Using Builder Pattern:**
```c#
var keyManagementService = KeyManagementService.NewBuilder()
    .WithLoggerFactory(loggerFactory) // Required - note: ILoggerFactory, not ILogger
    .WithRegionKeyArn("us-east-1", "arn_of_us-east-1") // Preferred region first
    .WithRegionKeyArn("us-east-2", "arn_of_us-east-2")
    .WithRegionKeyArn("us-west-2", "arn_of_us-west-2")
    .WithCredentials(myAwsCredentials)
    .Build();
```

**New Approach - Using Configuration (Recommended):**

First, add to your configuration (appsettings.json, etc.):
```json
{
  "AsherahKmsOptions": {
    "regionKeyArns": [
      {
        "region": "us-east-1",
        "keyArn": "arn_of_us-east-1"
      },
      {
        "region": "us-east-2",
        "keyArn": "arn_of_us-east-2"
      },
      {
        "region": "us-west-2",
        "keyArn": "arn_of_us-west-2"
      }
    ]
  }
}
```

Then in code:
```c#
// In DI setup
var kmsOptions = Configuration.GetValue<KeyManagementServiceOptions>("AsherahKmsOptions");
services.AddSingleton(kmsOptions);

// Later in your service
var keyManagementService = KeyManagementService.NewBuilder()
    .WithLoggerFactory(loggerFactory)
    .WithOptions(kmsOptions)
    .WithCredentials(awsOptions.GetCredentials())
    .Build();
```

#### Step 3: Handle Preferred Region

The old API took a preferred region as a constructor parameter. In the new API, you have two options:

**Option A: Order regions in your configuration/builder calls**
Simply place your preferred region first in the list. The first region will be tried first for data key generation.

**Option B: Use `OptimizeByRegions()` for runtime prioritization**
If you need to prioritize based on the current runtime region, use the `OptimizeByRegions()` method:

```c#
// Prioritize based on current AWS region at runtime
var optimizedKmsOptions = kmsOptions.OptimizeByRegions(awsOptions.Region.SystemName);

var keyManagementService = KeyManagementService.NewBuilder()
    .WithLoggerFactory(loggerFactory)
    .WithOptions(optimizedKmsOptions)
    .WithCredentials(awsOptions.GetCredentials())
    .Build();
```

#### Step 4: Update Logger Usage

**Old:**
```c#
.WithLogger(myLogger) // ILogger
```

**New:**
```c#
.WithLoggerFactory(loggerFactory) // ILoggerFactory - required
```

If you're using dependency injection, ensure `ILoggerFactory` is registered in your service container.

### Complete Migration Example

**Before:**
```c#
Dictionary<string, string> regionDictionary = new Dictionary<string, string>
{
    { "us-east-1", "arn:aws:kms:us-east-1:123456789012:key/abc" },
    { "us-west-2", "arn:aws:kms:us-west-2:234567890123:key/def" }
};

var keyManagementService = AwsKeyManagementServiceImpl.NewBuilder(regionDictionary, "us-east-1")
    .WithCredentials(credentials)
    .WithLogger(logger)
    .Build();
```

**After:**
```c#
var keyManagementService = KeyManagementService.NewBuilder()
    .WithLoggerFactory(loggerFactory)
    .WithRegionKeyArn("us-east-1", "arn:aws:kms:us-east-1:123456789012:key/abc")
    .WithRegionKeyArn("us-west-2", "arn:aws:kms:us-west-2:234567890123:key/def")
    .WithCredentials(credentials)
    .Build();
```

### Additional Notes

- The new `KeyManagementService` implements `IKeyManagementService`, so it's a drop-in replacement for `AwsKeyManagementServiceImpl`
- Both synchronous and asynchronous methods are available (`EncryptKey`/`EncryptKeyAsync`, `DecryptKey`/`DecryptKeyAsync`)
- The new implementation provides better support for dependency injection and configuration-based setup
- Region fallback behavior remains the same - if the first region fails, it will automatically try the next region in the list

## Upgrading to the new KeyMetastore Plugin

The legacy DynamoDB metastore (`DynamoDbMetastoreImpl`) implements `IMetastore<JObject>` and is used with the legacy `SessionFactory` in `GoDaddy.Asherah.AppEncryption`. The new KeyMetastore plugin (`DynamoDbMetastore` in `GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore`) implements `IKeyMetastore`, which is a breaking interface change: the new interface uses async methods, key records (`IKeyRecord`), and a different storage contract. **Because of this, upgrading to the new KeyMetastore plugin requires upgrading to the new SessionFactory** that accepts `IKeyMetastore` (in `GoDaddy.Asherah.AppEncryption.Core`). You cannot use the new KeyMetastore plugin with the legacy SessionFactory.

Follow the [SessionFactory Upgrade Guide](sessionfactory-upgrade-guide.md) to migrate to the Core `SessionFactory` and `IKeyMetastore`. Then switch your metastore implementation to the new plugin as below.

### Key Changes

1. **Interface**: `IMetastore<JObject>` (sync, JSON values) → `IKeyMetastore` (async, `IKeyRecord` values)
2. **Namespace**: Legacy DynamoDB metastore lives in `GoDaddy.Asherah.AppEncryption.Persistence`; the new plugin is in `GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore`
3. **Construction**: Region-based builder → builder that takes an `IAmazonDynamoDB` client and `DynamoDbMetastoreOptions` (e.g. table name, key suffix)
4. **SessionFactory**: Must use `GoDaddy.Asherah.AppEncryption.Core.SessionFactory` with `WithKeyMetastore()`; the legacy `SessionFactory` does not accept `IKeyMetastore`

### Migration Steps

#### Step 1: Upgrade to the new SessionFactory

Complete the migration described in the [SessionFactory Upgrade Guide](sessionfactory-upgrade-guide.md). Your code will use `GoDaddy.Asherah.AppEncryption.Core.SessionFactory` and `IKeyMetastore` instead of the legacy factory and `IMetastore<JObject>`.

#### Step 2: Replace DynamoDbMetastoreImpl with DynamoDbMetastore

**Old (legacy):**
```c#
using GoDaddy.Asherah.AppEncryption.Persistence;

IMetastore<JObject> metastore = DynamoDbMetastoreImpl.NewBuilder("us-west-2")
    .WithTableName("EncryptionKey")
    .WithRegion("us-west-2")
    .Build();

// Used with legacy SessionFactory
SessionFactory sessionFactory = SessionFactory.NewBuilder("product", "service")
    .WithMetastore(metastore)
    .WithCryptoPolicy(cryptoPolicy)
    .WithKeyManagementService(keyManagementService)
    .Build();
```

**New:**
```c#
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore;

var options = new DynamoDbMetastoreOptions
{
    KeyRecordTableName = "EncryptionKey",
    KeySuffix = ""  // optional; use for regional key suffix (e.g. global tables)
};

IKeyMetastore keyMetastore = DynamoDbMetastore.NewBuilder()
    .WithDynamoDbClient(amazonDynamoDbClient)
    .WithOptions(options)
    .Build();

SessionFactory sessionFactory = SessionFactory.NewBuilder("product", "service")
    .WithKeyMetastore(keyMetastore)
    .WithCryptoPolicy(cryptoPolicy)
    .WithKeyManagementService(keyManagementService)
    .WithLogger(logger)
    .Build();
```

You are responsible for creating and configuring `IAmazonDynamoDB` (region, credentials, endpoint, etc.). The new plugin does not construct the client from a region string.

#### Step 3: Table schema

The new and old DynamoDB metastore implementations expect the **exact same schema**. They are compatible: the new `DynamoDbMetastore` plugin will work with your existing key table with no schema changes.

### Summary

- **Upgrade order:** Migrate to the new SessionFactory and `IKeyMetastore` first (see [SessionFactory Upgrade Guide](sessionfactory-upgrade-guide.md)), then replace the legacy DynamoDB metastore with `DynamoDbMetastore` from `GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore`.
- The new KeyMetastore plugin cannot be used with the legacy `SessionFactory` or `IMetastore<JObject>`-based code paths.
