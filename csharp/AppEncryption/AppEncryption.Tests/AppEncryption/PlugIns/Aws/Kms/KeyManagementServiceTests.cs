using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    [ExcludeFromCodeCoverage]
    public class KeyManagementServiceTests : IDisposable
    {
        private const string UsEast1 = "us-east-1";
        private const string ArnUsEast1 = "arn-us-east-1";
        private const string UsWest2 = "us-west-2";
        private const string ArnUsWest2 = "arn-us-west-2";
        private const string EuWest2 = "eu-west-2";
        private const string ArnEuWest2 = "arn-eu-west-2";

        private readonly KeyManagementService _keyManagementServiceEast;
        private readonly KeyManagementService _keyManagementServiceWest;
        private readonly KeyManagementService _keyManagementServiceAdditionalRegion;
        private readonly KeyManagementService _keyManagementServiceSomeBroken;
        private readonly KeyManagementService _keyManagementServiceAllBroken;
        private readonly KeyManagementService _keyManagementServiceOnlyEurope;

        public KeyManagementServiceTests()
        {
            var optionsEast = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = UsEast1, KeyArn = ArnUsEast1 },
                    new RegionKeyArn { Region = UsWest2, KeyArn = ArnUsWest2 }
                ]
            };

            var optionsWest = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = UsWest2, KeyArn = ArnUsWest2 },
                    new RegionKeyArn { Region = UsEast1, KeyArn = ArnUsEast1 }
                ]
            };

            var optionsEurope = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = EuWest2, KeyArn = ArnEuWest2 },
                    new RegionKeyArn { Region = UsWest2, KeyArn = ArnUsWest2 },
                    new RegionKeyArn { Region = UsEast1, KeyArn = ArnUsEast1 }
                ]
            };

            var optionsOnlyEurope = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = EuWest2, KeyArn = ArnEuWest2 }
                ]
            };

            var optionsSomeBroken = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = UsEast1, KeyArn = "ERROR" },
                    new RegionKeyArn { Region = UsWest2, KeyArn = ArnUsWest2 }
                ]
            };

            var optionsAllBroken = new KeyManagementServiceOptions
            {
                RegionKeyArns =
                [
                    new RegionKeyArn { Region = UsEast1, KeyArn = "ERROR" },
                    new RegionKeyArn { Region = UsWest2, KeyArn = "ERROR" }
                ]
            };

            var loggerFactoryStub = new LoggerFactoryStub();
            var clientFactoryStub = new KeyManagementClientFactoryStub(optionsEast);

            _keyManagementServiceEast = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsEast)
                .WithKmsClientFactory(clientFactoryStub)
                .Build();

            _keyManagementServiceWest = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsWest)
                .WithKmsClientFactory(clientFactoryStub)
                .Build();

            var clientFactoryStubSomeBroken = new KeyManagementClientFactoryStub(optionsSomeBroken);
            _keyManagementServiceSomeBroken = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsSomeBroken)
                .WithKmsClientFactory(clientFactoryStubSomeBroken)
                .Build();

            var clientFactoryStubAllBroken = new KeyManagementClientFactoryStub(optionsAllBroken);
            _keyManagementServiceAllBroken = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsAllBroken)
                .WithKmsClientFactory(clientFactoryStubAllBroken)
                .Build();

            var clientFactoryStubEurope = new KeyManagementClientFactoryStub(optionsEurope);
            _keyManagementServiceAdditionalRegion = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsEurope)
                .WithKmsClientFactory(clientFactoryStubEurope)
                .Build();

            var clientFactoryStubOnlyEurope = new KeyManagementClientFactoryStub(optionsOnlyEurope);
            _keyManagementServiceOnlyEurope = KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactoryStub)
                .WithOptions(optionsOnlyEurope)
                .WithKmsClientFactory(clientFactoryStubOnlyEurope)
                .Build();
        }

        [Fact]
        public async Task EncryptKeyAsync_ShouldEncryptKey()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = await _keyManagementServiceEast.EncryptKeyAsync(key);

            // Assert
            ValidateEncryptedKey(result);
        }

        [Fact]
        public async Task EncryptKeyAsync_RegionFallback_Succeeds()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = await _keyManagementServiceEast.EncryptKeyAsync(key);

            // Assert
            ValidateEncryptedKey(result);
        }

        [Fact]
        public void EncryptKey_ShouldEncryptKey()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = _keyManagementServiceEast.EncryptKey(key);

            // Assert
            ValidateEncryptedKey(result);
        }

        private static void ValidateEncryptedKey(byte[] encryptedKeyResult)
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
            Assert.Equal(2, kmsKeksArray.Count); // Should have 2 regions

            // Assert each KMS KEK has required properties
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
                Assert.True(regionValue == "us-east-1" || regionValue == "us-west-2");

                // Assert arn exists
                Assert.True(kekObject.ContainsKey("arn"));
                var arn = kekObject["arn"];
                Assert.NotNull(arn);
                var arnValue = arn!.AsValue().GetValue<string>();
                Assert.NotNull(arnValue);
                Assert.True(arnValue == "arn-us-east-1" || arnValue == "arn-us-west-2" || arnValue == "ERROR");

                // Assert encryptedKek exists and is not empty
                Assert.True(kekObject.ContainsKey("encryptedKek"));
                var encryptedKek = kekObject["encryptedKek"];
                Assert.NotNull(encryptedKek);
                var encryptedKekValue = encryptedKek!.AsValue().GetValue<string>();
                Assert.NotNull(encryptedKekValue);
                Assert.NotEmpty(encryptedKekValue);
            }

            // Assert we have both regions
            var regions = kmsKeksArray.Select(kek => kek!.AsObject()["region"]!.AsValue().GetValue<string>()).ToList();
            Assert.Contains("us-east-1", regions);
            Assert.Contains("us-west-2", regions);
        }


        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act
            var encryptedResult = await _keyManagementServiceEast.EncryptKeyAsync(originalKey);
            var decryptedKey = await _keyManagementServiceEast.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public void DecryptKey_ShouldDecryptKey()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act
            var encryptedResult = _keyManagementServiceEast.EncryptKey(originalKey);
            var decryptedKey = _keyManagementServiceEast.DecryptKey(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey_BetweenRegions()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = await _keyManagementServiceEast.EncryptKeyAsync(originalKey);
            var decryptedKey = await _keyManagementServiceWest.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldDecryptKey_BetweenRegions_WhenNewRegionExists()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = await _keyManagementServiceEast.EncryptKeyAsync(originalKey);
            var decryptedKey = await _keyManagementServiceAdditionalRegion.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task DecryptKeyAsync_ShouldFail_BetweenRegions_WhenNoRegionsMatch()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = await _keyManagementServiceEast.EncryptKeyAsync(originalKey);
            var decryptTask = _keyManagementServiceOnlyEurope.DecryptKeyAsync(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            await Assert.ThrowsAsync<KmsException>(async () => await decryptTask);
        }

        [Fact]
        public void DecryptKey_ShouldDecryptKey_BetweenRegions()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var originalKey = crypto.GenerateKey(keyCreationTime);

            // Act - Encrypt with East, decrypt with West
            var encryptedResult = _keyManagementServiceEast.EncryptKey(originalKey);
            var decryptedKey = _keyManagementServiceWest.DecryptKey(encryptedResult, keyCreationTime, revoked: false);

            // Assert
            Assert.NotNull(decryptedKey);
            Assert.Equal(originalKey.WithKey(keyBytes => keyBytes), decryptedKey.WithKey(keyBytes => keyBytes));
            Assert.Equal(originalKey.GetCreated(), decryptedKey.GetCreated());
        }

        [Fact]
        public async Task EncryptKeyAsync_AllRegionsBroken_ShouldThrowException()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act & Assert
            await Assert.ThrowsAsync<KmsException>(async () =>
                await _keyManagementServiceAllBroken.EncryptKeyAsync(key));
        }

        [Fact]
        public async Task EncryptKeyAsync_SomeRegionsBroken_Succeeds()
        {
            // Arrange
            using var crypto = new BouncyAes256GcmCrypto();
            var keyCreationTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
            using var key = crypto.GenerateKey(keyCreationTime);

            // Act
            var result = await _keyManagementServiceSomeBroken.EncryptKeyAsync(key);

            // Assert
            ValidateEncryptedKey(result);
        }

        [Fact]
        public async Task EncryptKeyAsync_Fails_WhenCryptoKeyIsNull()
        {
            await Assert.ThrowsAsync<KmsException>(async () =>
                await _keyManagementServiceEast.EncryptKeyAsync(null!));
        }

        public void Dispose()
        {
            _keyManagementServiceEast?.Dispose();
            _keyManagementServiceWest?.Dispose();
        }
    }
}
