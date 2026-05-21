# Plugins Upgrade Guide

This guide provides step-by-step instructions for upgrading from obsolete plugin implementations to the new recommended plugins.

## Table of Contents

- [Upgrading to the new KeyManagementService Plugin](#upgrading-to-the-new-keymanagementservice-plugin)

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
