# NuGet Package Version Check Report

## Summary
This report shows all NuGet packages across all .csproj files and identifies which ones have newer versions available.

## Packages with Newer Versions Available

### AppEncryption/AppEncryption.csproj
- **AWSSDK.DynamoDBv2**: Current `4.0.9.5` → Latest `4.0.9.6` ⚠️
- **AWSSDK.KeyManagementService**: Current `4.0.7` → Latest `4.0.7.1` ⚠️

### AppEncryption.PlugIns.Aws/AppEncryption.PlugIns.Aws.csproj
- **AWSSDK.DynamoDBv2**: Current `4.0.7.2` → Latest `4.0.9.6` ⚠️ (major update available)
- **AWSSDK.KeyManagementService**: Current `4.0.4.9` → Latest `4.0.7.1` ⚠️ (major update available)
- **Microsoft.Extensions.Logging.Abstractions**: Current `9.0.9` → Latest `10.0.0` ⚠️ (major update available)

### AppEncryption.Tests/AppEncryption.Tests.csproj
- **AWSSDK.SecurityToken**: Current `4.0.4` → Latest `4.0.4.1` ⚠️

### AppEncryption.IntegrationTests/AppEncryption.IntegrationTests.csproj
- **AWSSDK.SecurityToken**: Current `4.0.4` → Latest `4.0.4.1` ⚠️

## Packages Up to Date

### Crypto/Crypto.csproj
- BouncyCastle.NetCore: `2.2.1` ✓
- GoDaddy.Asherah.SecureMemory: `0.4.0` ✓
- System.Text.Encodings.Web: `10.0.0` ✓
- System.Text.Json: `10.0.0` ✓

### AppEncryption/AppEncryption.csproj
- LanguageExt.Core: `4.4.9` ✓
- Microsoft.Extensions.Caching.Memory: `10.0.0` ✓
- Microsoft.Extensions.Logging.Abstractions: `10.0.0` ✓
- Newtonsoft.Json: `13.0.4` ✓
- App.Metrics: `4.3.0` ✓
- System.Text.Encodings.Web: `10.0.0` ✓
- System.Text.Json: `10.0.0` ✓

### AppEncryption.Tests/AppEncryption.Tests.csproj
- JunitXml.TestLogger: `7.0.2` ✓
- coverlet.msbuild: `6.0.4` ✓
- Microsoft.Extensions.Logging.Console: `10.0.0` ✓
- Microsoft.NET.Test.Sdk: `18.0.1` ✓
- Moq: `4.20.72` ✓
- MySql.Data: `9.5.0` ✓
- Testcontainers.DynamoDb: `4.8.1` ✓
- Testcontainers.MySql: `4.8.1` ✓
- xunit: `2.9.3` ✓
- xunit.runner.visualstudio: `3.1.5` ✓
- Xunit.SkippableFact: `1.5.23` ✓

### AppEncryption.IntegrationTests/AppEncryption.IntegrationTests.csproj
- coverlet.msbuild: `6.0.4` ✓
- MySql.Data: `9.5.0` ✓
- NetEscapades.Configuration.Yaml: `3.1.0` ✓
- Microsoft.Extensions.Logging.Console: `10.0.0` ✓
- Microsoft.NET.Test.Sdk: `18.0.1` ✓
- Moq: `4.20.72` ✓
- xunit: `2.9.3` ✓
- xunit.runner.visualstudio: `3.1.5` ✓
- Xunit.SkippableFact: `1.5.23` ✓

## Recommendations

1. **High Priority**: Update `AppEncryption.PlugIns.Aws` project packages:
   - AWSSDK.DynamoDBv2: `4.0.7.2` → `4.0.9.6`
   - AWSSDK.KeyManagementService: `4.0.4.9` → `4.0.7.1`
   - Microsoft.Extensions.Logging.Abstractions: `9.0.9` → `10.0.0`

2. **Medium Priority**: Minor updates:
   - AppEncryption: AWSSDK.DynamoDBv2 `4.0.9.5` → `4.0.9.6`
   - AppEncryption: AWSSDK.KeyManagementService `4.0.7` → `4.0.7.1`
   - Tests: AWSSDK.SecurityToken `4.0.4` → `4.0.4.1` (in both test projects)

## Notes
- All other packages are at their latest versions
- The AWS SDK packages in the PlugIns.Aws project are significantly behind and should be updated to match the versions in the main AppEncryption project
