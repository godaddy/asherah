using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    [ExcludeFromCodeCoverage]
    public class KeyManagementServiceTests
    {
        private const string UsEast1 = "us-east-1";
        private const string ArnUsEast1 = "arn-us-east-1";
        private const string UsWest2 = "us-west-2";
        private const string ArnUsWest2 = "arn-us-west-2";
        private const string EuWest2 = "eu-west-2";
        private const string ArnEuWest2 = "arn-eu-west-2";

        [Fact]
        public async Task EncryptKeyAsync_ShouldEncryptKey()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = await service.EncryptKeyAsync(key);

            // Assert
            ValidateEncryptedKey(result, UsEast1, UsWest2);
        }

        [Fact]
        public void EncryptKey_ShouldEncryptKey()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = service.EncryptKey(key);

            // Assert
            ValidateEncryptedKey(result, UsEast1, UsWest2);
        }

        private static void ValidateEncryptedKey(byte[] encryptedKeyResult, params string[] expectedRegions)
        {
            Assert.NotNull(encryptedKeyResult);

            // Deserialize and validate JSON structure
            var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(encryptedKeyResult);

            // Assert JSON structure
            Assert.NotNull(jsonNode);
            Assert.True(jsonNode is System.Text.Json.Nodes.JsonObject);

            var jsonObject = jsonNode.AsObject();

            // Assert encryptedKey exists and is not empty
            Assert.True(jsonObject.ContainsKey("encryptedKey"));
            var encryptedKey = jsonObject["encryptedKey"];
            Assert.NotNull(encryptedKey);
            Assert.True(encryptedKey is System.Text.Json.Nodes.JsonValue);
            var encryptedKeyValue = encryptedKey!.AsValue().GetValue<string>();
            Assert.NotNull(encryptedKeyValue);
            Assert.NotEmpty(encryptedKeyValue);

            // Assert kmsKeks exists and is an array
            Assert.True(jsonObject.ContainsKey("kmsKeks"));
            var kmsKeks = jsonObject["kmsKeks"];
            Assert.NotNull(kmsKeks);
            Assert.True(kmsKeks is System.Text.Json.Nodes.JsonArray);

            var kmsKeksArray = kmsKeks!.AsArray();
            Assert.Equal(expectedRegions.Length, kmsKeksArray.Count);

            // Assert each KMS KEK has required properties
            var actualRegions = new List<string>();
            foreach (var kekNode in kmsKeksArray)
            {
                Assert.NotNull(kekNode);
                Assert.True(kekNode is System.Text.Json.Nodes.JsonObject);

                var kekObject = kekNode!.AsObject();

                // Assert region exists
                Assert.True(kekObject.ContainsKey("region"));
                var region = kekObject["region"];
                Assert.NotNull(region);
                var regionValue = region!.AsValue().GetValue<string>();
                Assert.NotNull(regionValue);
                actualRegions.Add(regionValue);

                // Assert arn exists
                Assert.True(kekObject.ContainsKey("arn"));
                var arn = kekObject["arn"];
                Assert.NotNull(arn);
                var arnValue = arn!.AsValue().GetValue<string>();
                Assert.NotNull(arnValue);

                // Assert encryptedKek exists and is not empty
                Assert.True(kekObject.ContainsKey("encryptedKek"));
                var encryptedKek = kekObject["encryptedKek"];
                Assert.NotNull(encryptedKek);
                var encryptedKekValue = encryptedKek!.AsValue().GetValue<string>();
                Assert.NotNull(encryptedKekValue);
                Assert.NotEmpty(encryptedKekValue);
            }

            // Assert we have all expected regions
            foreach (var expectedRegion in expectedRegions)
            {
                Assert.Contains(expectedRegion, actualRegions);
            }
        }


        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act
            var encryptedResult = await service.EncryptKeyAsync(originalKey);
            var decryptedKey = await service.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public void DecryptKey_ShouldDecryptKey()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act
            var encryptedResult = service.EncryptKey(originalKey);
            var decryptedKey = service.DecryptKey(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey_BetweenRegions()
        {
            // Arrange
            using var serviceEast = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var serviceWest = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsWest2, ArnUsWest2), (UsEast1, ArnUsEast1))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = await serviceEast.EncryptKeyAsync(originalKey);
            var decryptedKey = await serviceWest.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey_BetweenRegions_WhenNewRegionExists()
        {
            // Arrange
            using var serviceEast = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var serviceWithAdditionalRegion = KeyManagementServiceTestBuilder.Create()
                .WithRegions((EuWest2, ArnEuWest2), (UsWest2, ArnUsWest2), (UsEast1, ArnUsEast1))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with service that has additional region
            var encryptedResult = await serviceEast.EncryptKeyAsync(originalKey);
            var decryptedKey = await serviceWithAdditionalRegion.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldFail_BetweenRegions_WhenNoRegionsMatch()
        {
            // Arrange
            using var serviceEast = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var serviceOnlyEurope = KeyManagementServiceTestBuilder.Create()
                .WithRegions((EuWest2, ArnEuWest2))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with Europe-only service
            var encryptedResult = await serviceEast.EncryptKeyAsync(originalKey);
            var decryptTask = serviceOnlyEurope.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            await Assert.ThrowsAsync<KmsException>(async () => await decryptTask);
        }

        [Fact]
        public void DecryptKey_ShouldDecryptKey_BetweenRegions()
        {
            // Arrange
            using var serviceEast = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();
            using var serviceWest = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsWest2, ArnUsWest2), (UsEast1, ArnUsEast1))
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = serviceEast.EncryptKey(originalKey);
            var decryptedKey = serviceWest.DecryptKey(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task EncryptKeyAsync_AllRegionsBroken_ShouldThrowException()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .WithBrokenRegions(UsEast1, UsWest2)
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act & Assert
            await Assert.ThrowsAsync<KmsException>(async () =>
                await service.EncryptKeyAsync(key));
        }

        [Fact]
        public async Task EncryptKeyAsync_SomeRegionsBroken_Succeeds()
        {
            // Arrange
            var loggerFactory = new LoggerFactoryStub();
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .WithBrokenRegion(UsEast1)
                .WithLoggerFactory(loggerFactory)
                .Build();
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = await service.EncryptKeyAsync(key);

            // Assert
            ValidateEncryptedKey(result, UsEast1, UsWest2);

            // Verify that a warning was logged for the failed region
            var warningLogs = loggerFactory.LogEntries
                .Where(log => log.LogLevel == LogLevel.Warning)
                .ToList();
            Assert.NotEmpty(warningLogs);
            var failedRegionLog = warningLogs.FirstOrDefault(log =>
                log.Message.Contains(UsEast1) && log.Message.Contains("Failed to generate data key"));
            Assert.NotNull(failedRegionLog);
            Assert.NotNull(failedRegionLog.Exception);
        }

        [Fact]
        public async Task EncryptKeyAsync_Fails_WhenCryptoKeyIsNull()
        {
            // Arrange
            using var service = KeyManagementServiceTestBuilder.Create()
                .WithRegions((UsEast1, ArnUsEast1), (UsWest2, ArnUsWest2))
                .Build();

            // Act & Assert
            await Assert.ThrowsAsync<KmsException>(async () =>
                await service.EncryptKeyAsync(null!));
        }
    }
}
