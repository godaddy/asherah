using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Amazon.Runtime.SharedInterfaces;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using LanguageExt;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

using static GoDaddy.Asherah.AppEncryption.Kms.AwsKeyManagementServiceImpl;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Kms
{
    [Collection("Logger Fixture collection")]
    public class AwsKeyManagementServiceImplTest : IClassFixture<MetricsFixture>
    {
        private const string UsEast1 = "us-east-1";
        private const string ArnUsEast1 = "arn-us-east-1";
        private const string UsWest1 = "us-west-1";
        private const string ArnUsWest1 = "arn-us-west-1";

        private readonly Dictionary<string, string> regionToArnDictionary = new Dictionary<string, string>
        {
            { UsEast1, ArnUsEast1 },
            { UsWest1, ArnUsWest1 },
        };

        private readonly string preferredRegion = UsWest1;

        private readonly Mock<IAmazonKeyManagementService> amazonKeyManagementServiceClientMock;
        private readonly Mock<AeadEnvelopeCrypto> cryptoMock;
        private readonly Mock<AwsKmsClientFactory> awsKmsClientFactoryMock;
        private readonly Mock<CryptoKey> cryptoKeyMock;
        private readonly Mock<AwsKeyManagementServiceImpl> awsKeyManagementServiceImplSpy;

        public AwsKeyManagementServiceImplTest()
        {
            amazonKeyManagementServiceClientMock = new Mock<IAmazonKeyManagementService>();
            cryptoMock = new Mock<AeadEnvelopeCrypto>();
            awsKmsClientFactoryMock = new Mock<AwsKmsClientFactory>();
            cryptoKeyMock = new Mock<CryptoKey>();

            awsKmsClientFactoryMock.Setup(x => x.CreateAwsKmsClient(It.IsAny<string>()))
                .Returns(amazonKeyManagementServiceClientMock.Object);
            awsKeyManagementServiceImplSpy = new Mock<AwsKeyManagementServiceImpl>(
                regionToArnDictionary,
                preferredRegion,
                cryptoMock.Object,
                awsKmsClientFactoryMock.Object) { CallBase = true };
        }

        [Fact]
        private void TestRegionToArnAndClientDictionaryGeneration()
        {
            AwsKeyManagementServiceImpl awsKeyManagementService = new AwsKeyManagementServiceImpl(
                regionToArnDictionary,
                preferredRegion,
                cryptoMock.Object,
                awsKmsClientFactoryMock.Object);
            IDictionaryEnumerator dictionaryEnumerator =
                awsKeyManagementService.RegionToArnAndClientDictionary.GetEnumerator();
            dictionaryEnumerator.MoveNext();
            DictionaryEntry dictionaryEnumeratorEntry = dictionaryEnumerator.Entry;
            Assert.Equal(preferredRegion, dictionaryEnumeratorEntry.Key);
            Assert.Equal(regionToArnDictionary.Count, awsKeyManagementService.RegionToArnAndClientDictionary.Count);
        }

        [Fact]
        private void TestDecryptKeySuccessful()
        {
            byte[] encryptedKey = { 0, 1 };
            byte[] kmsKeyEncryptionKey = { 2, 3 };

            JObject kmsKeyEnvelopeTest = JObject.FromObject(new Dictionary<string, object>
            {
                { EncryptedKey, Convert.ToBase64String(encryptedKey) },
                {
                    KmsKeksKey, new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { RegionKey, UsWest1 },
                            { ArnKey, ArnUsWest1 },
                            { EncryptedKek, Convert.ToBase64String(kmsKeyEncryptionKey) },
                        },
                    }
                },
            });

            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool revoked = false;
            awsKeyManagementServiceImplSpy
                .Setup(x => x.DecryptKmsEncryptedKey(
                    amazonKeyManagementServiceClientMock.Object,
                    encryptedKey,
                    now,
                    kmsKeyEncryptionKey,
                    revoked))
                .Returns(cryptoKeyMock.Object);

            CryptoKey actualCryptoKey =
                awsKeyManagementServiceImplSpy.Object.DecryptKey(
                    new Asherah.AppEncryption.Util.Json(kmsKeyEnvelopeTest).ToUtf8(), now, revoked);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestDecryptKeyWithMissingRegionInPayloadShouldSkipAndSucceed()
        {
            byte[] encryptedKey = { 0, 1 };
            byte[] kmsKeyEncryptionKey = { 2, 3 };

            JObject kmsKeyEnvelope = JObject.FromObject(new Dictionary<string, object>
            {
                { EncryptedKey, Convert.ToBase64String(encryptedKey) },
                {
                    KmsKeksKey, new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { RegionKey, "some_region" },  // should appear before valid us-east region
                            { ArnKey, "some_arn" },
                            { EncryptedKek, Convert.ToBase64String(kmsKeyEncryptionKey) },
                        },
                        new Dictionary<string, object>
                        {
                            { RegionKey, UsEast1 },
                            { ArnKey, ArnUsEast1 },
                            { EncryptedKek, Convert.ToBase64String(kmsKeyEncryptionKey) },
                        },
                    }
                },
            });

            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool revoked = false;
            awsKeyManagementServiceImplSpy
                .Setup(x => x.DecryptKmsEncryptedKey(
                    amazonKeyManagementServiceClientMock.Object,
                    encryptedKey,
                    now,
                    kmsKeyEncryptionKey,
                    revoked))
                .Returns(cryptoKeyMock.Object);

            CryptoKey actualCryptoKey =
                awsKeyManagementServiceImplSpy.Object.DecryptKey(
                    new Asherah.AppEncryption.Util.Json(kmsKeyEnvelope).ToUtf8(), now, revoked);
            Assert.Equal(cryptoKeyMock.Object, actualCryptoKey);
        }

        [Fact]
        private void TestDecryptKeyWithKmsFailureShouldThrowKmsException()
        {
            byte[] encryptedKey = { 0, 1 };
            byte[] kmsKeyEncryptionKey = { 2, 3 };
            JObject kmsKeyEnvelope = JObject.FromObject(new Dictionary<string, object>
            {
                { EncryptedKey, Convert.ToBase64String(encryptedKey) },
                {
                    KmsKeksKey, new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { RegionKey, UsWest1 },
                            { ArnKey, ArnUsWest1 },
                            { EncryptedKek, Convert.ToBase64String(kmsKeyEncryptionKey) },
                        },
                    }
                },
            });

            awsKeyManagementServiceImplSpy
                .Setup(x => x.DecryptKmsEncryptedKey(
                    It.IsAny<IAmazonKeyManagementService>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<bool>()))
                .Throws<AmazonServiceException>();
            Assert.Throws<KmsException>(() =>
                awsKeyManagementServiceImplSpy.Object.DecryptKey(
                    new Asherah.AppEncryption.Util.Json(kmsKeyEnvelope).ToUtf8(), DateTimeOffset.UtcNow, false));
        }

        [Fact]
        private void TestGetPrioritizedKmsRegionKeyJsonList()
        {
            string json =
                $@"[{{ '{RegionKey}':'{UsEast1}' }}, {{ '{RegionKey}':'a' }},
                    {{ '{RegionKey}':'zzzzzz' }}, {{ '{RegionKey}':'{preferredRegion}' }}]";

            // region 'a' should always be lexicographically first and region 'zzzzzz' should always be
            // lexicographically last
            JArray regionArray = JArray.Parse(json);
            List<Asherah.AppEncryption.Util.Json> ret =
                awsKeyManagementServiceImplSpy.Object.GetPrioritizedKmsRegionKeyJsonList(regionArray);
            Assert.Equal(preferredRegion, ret[0].GetString(RegionKey));

            // If we ever add geo awareness, add unit tests appropriately
        }

        [Fact]
        private void TestDecryptKmsEncryptedKeySuccessful()
        {
            byte[] cipherText = { 0, 1 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            byte[] keyEncryptionKey = { 2, 3 };
            bool revoked = false;
            byte[] plaintextBackingBytes = { 4, 5 };
            amazonKeyManagementServiceClientMock.Setup(x => x.Decrypt(It.IsAny<byte[]>(), null))
                .Returns(plaintextBackingBytes);
            cryptoMock.Setup(x => x.GenerateKeyFromBytes(plaintextBackingBytes)).Returns(cryptoKeyMock.Object);
            Mock<CryptoKey> expectedKey = new Mock<CryptoKey>();
            cryptoMock.Setup(x => x.DecryptKey(cipherText, now, cryptoKeyMock.Object, revoked))
                .Returns(expectedKey.Object);
            CryptoKey actualKey = awsKeyManagementServiceImplSpy.Object.DecryptKmsEncryptedKey(
                amazonKeyManagementServiceClientMock.Object, cipherText, now, keyEncryptionKey, revoked);
            Assert.Equal(expectedKey.Object, actualKey);
            Assert.Equal(new byte[] { 0, 0 }, plaintextBackingBytes);
        }

        [Fact]
        private void TestDecryptKmsEncryptedKeyWithKmsFailureShouldThrowException()
        {
            byte[] cipherText = { 0, 1 };
            byte[] keyEncryptionKey = { 2, 3 };
            amazonKeyManagementServiceClientMock.Setup(x => x.Decrypt(It.IsAny<byte[]>(), null))
                .Throws<AmazonServiceException>();
            Assert.Throws<AmazonServiceException>(() =>
                awsKeyManagementServiceImplSpy.Object.DecryptKmsEncryptedKey(
                    amazonKeyManagementServiceClientMock.Object,
                    cipherText,
                    DateTimeOffset.UtcNow,
                    keyEncryptionKey,
                    false));
        }

        [Fact]
        private void TestDecryptKmsEncryptedKeyWithCryptoFailureShouldThrowExceptionAndWipeBytes()
        {
            byte[] cipherText = { 0, 1 };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            byte[] keyEncryptionKey = { 2, 3 };
            bool revoked = false;
            byte[] plaintextBackingBytes = { 4, 5 };
            amazonKeyManagementServiceClientMock.Setup(x => x.Decrypt(It.IsAny<byte[]>(), null))
                .Returns(plaintextBackingBytes);
            cryptoMock.Setup(x => x.GenerateKeyFromBytes(plaintextBackingBytes)).Returns(cryptoKeyMock.Object);
            cryptoMock.Setup(x => x.DecryptKey(cipherText, now, cryptoKeyMock.Object, revoked))
                .Throws(new AppEncryptionException("fake error"));

            Assert.Throws<AppEncryptionException>(() =>
                awsKeyManagementServiceImplSpy.Object.DecryptKmsEncryptedKey(
                    amazonKeyManagementServiceClientMock.Object, cipherText, now, keyEncryptionKey, revoked));
            Assert.Equal(new byte[] { 0, 0 }, plaintextBackingBytes);
        }

        [Fact]
        private void TestPrimaryBuilderPath()
        {
            AwsKeyManagementServiceImpl.Builder awsKeyManagementServicePrimaryBuilder =
                NewBuilder(regionToArnDictionary, preferredRegion);
            AwsKeyManagementServiceImpl awsKeyManagementServiceBuilder = awsKeyManagementServicePrimaryBuilder.Build();
            Assert.NotNull(awsKeyManagementServiceBuilder);
        }

        [Fact]
        private void TestGenerateDataKeySuccessful()
        {
            OrderedDictionary sortedRegionToArnAndClient =
                awsKeyManagementServiceImplSpy.Object.RegionToArnAndClientDictionary;
            Mock<GenerateDataKeyResult> dataKeyResultMock = new Mock<GenerateDataKeyResult>();

            // preferred region's ARN, verify it's the first and hence returned
            amazonKeyManagementServiceClientMock
                .Setup(x => x.GenerateDataKey(ArnUsWest1, null, DataKeySpec.AES_256))
                .Returns(dataKeyResultMock.Object);
            GenerateDataKeyResult dataKeyResponseActual =
                awsKeyManagementServiceImplSpy.Object.GenerateDataKey(sortedRegionToArnAndClient, out _);
            Assert.Equal(dataKeyResultMock.Object, dataKeyResponseActual);
        }

        [Fact]
        private void TestGenerateDataKeyWithKmsFailureShouldThrowKmsException()
        {
            OrderedDictionary sortedRegionToArnAndClient =
                awsKeyManagementServiceImplSpy.Object.RegionToArnAndClientDictionary;
            amazonKeyManagementServiceClientMock
                .Setup(x => x.GenerateDataKey(It.IsAny<string>(), null, It.IsAny<string>()))
                .Throws<AmazonServiceException>();
            Assert.Throws<KmsException>(() =>
                awsKeyManagementServiceImplSpy.Object.GenerateDataKey(sortedRegionToArnAndClient, out _));
        }

        [Fact]
        private void TestEncryptKeyAndBuildResult()
        {
            byte[] encryptedKey = { 0, 1 };
            EncryptResponse encryptResponse = new EncryptResponse
            {
                CiphertextBlob = new MemoryStream(encryptedKey),
            };
            byte[] dataKeyPlainText = { 2, 3 };
            amazonKeyManagementServiceClientMock
                .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(encryptResponse));

            Option<JObject> actualResult = awsKeyManagementServiceImplSpy.Object.EncryptKeyAndBuildResult(
                amazonKeyManagementServiceClientMock.Object, preferredRegion, ArnUsWest1, dataKeyPlainText);

            Assert.Equal(preferredRegion, ((JObject)actualResult).GetValue(RegionKey).ToString());
            Assert.Equal(ArnUsWest1, ((JObject)actualResult).GetValue(ArnKey).ToString());
            Assert.Equal(
                encryptedKey, Convert.FromBase64String(((JObject)actualResult).GetValue(EncryptedKek).ToString()));
        }

        [Fact]
        private void TestEncryptKeyAndBuildResultReturnEmptyOptional()
        {
            byte[] dataKeyPlainText = { 0, 1 };
            amazonKeyManagementServiceClientMock
                .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), It.IsAny<CancellationToken>()))
                .Throws<AggregateException>();
            Option<JObject> actualResult = awsKeyManagementServiceImplSpy.Object.EncryptKeyAndBuildResult(
                amazonKeyManagementServiceClientMock.Object,
                preferredRegion,
                ArnUsWest1,
                dataKeyPlainText);

            Assert.Equal(Option<JObject>.None, actualResult);
        }

        [Fact]
        private void TestEncryptKeySuccessful()
        {
            byte[] encryptedKey = { 3, 4 };
            byte[] dataKeyPlainText = { 1, 2 };
            byte[] dataKeyCipherText = { 5, 6 };
            byte[] encryptKeyCipherText = { 7, 8 };

            JObject encryptKeyAndBuildResultJson = JObject.FromObject(new Dictionary<string, object>
            {
                { RegionKey, UsEast1 },
                { ArnKey, ArnUsEast1 },
                { EncryptedKek, Convert.ToBase64String(encryptKeyCipherText) },
            });

            JObject kmsKeyEnvelope = JObject.FromObject(new Dictionary<string, object>
            {
                { EncryptedKey, Convert.ToBase64String(encryptedKey) },
                {
                    // For some reason we have to use ConcurrentBag here. Likely due to internal ConcurrentBag list
                    // structure failing to compare against regular List?
                    KmsKeksKey, new ConcurrentBag<object>
                    {
                        new Dictionary<string, object>
                        {
                            { RegionKey, UsWest1 },
                            { ArnKey, ArnUsWest1 },
                            { EncryptedKek, Convert.ToBase64String(dataKeyCipherText) },
                        },
                        encryptKeyAndBuildResultJson,
                    }
                },
            });
            GenerateDataKeyResult generateDataKeyResult = new GenerateDataKeyResult
            {
                KeyPlaintext = dataKeyPlainText,
                KeyCiphertext = dataKeyCipherText,
            };

            Mock<CryptoKey> generatedDataKeyCryptoKey = new Mock<CryptoKey>();
            string keyId = ArnUsWest1;

            awsKeyManagementServiceImplSpy
                .Setup(x =>
                    x.GenerateDataKey(awsKeyManagementServiceImplSpy.Object.RegionToArnAndClientDictionary, out keyId))
                .Returns(generateDataKeyResult);
            cryptoMock.Setup(x => x.GenerateKeyFromBytes(generateDataKeyResult.KeyPlaintext))
                .Returns(generatedDataKeyCryptoKey.Object);
            cryptoMock.Setup(x => x.EncryptKey(cryptoKeyMock.Object, generatedDataKeyCryptoKey.Object))
                .Returns(encryptedKey);
            awsKeyManagementServiceImplSpy.Setup(x =>
                    x.EncryptKeyAndBuildResult(
                        It.IsAny<IAmazonKeyManagementService>(),
                        UsEast1,
                        ArnUsEast1,
                        dataKeyPlainText))
                .Returns(Option<JObject>.Some(encryptKeyAndBuildResultJson));

            byte[] encryptedResult = awsKeyManagementServiceImplSpy.Object.EncryptKey(cryptoKeyMock.Object);
            Asherah.AppEncryption.Util.Json kmsKeyEnvelopeResult = new Asherah.AppEncryption.Util.Json(encryptedResult);

            Assert.True(JToken.DeepEquals(kmsKeyEnvelope, kmsKeyEnvelopeResult.ToJObject()));
            Assert.Equal(new byte[] { 0, 0 }, dataKeyPlainText);
        }

        [Fact]
        private void TestEncryptKeyShouldThrowExceptionAndWipeBytes()
        {
            byte[] dataKeyPlainText = { 1, 2 };
            byte[] encryptedKey = { 3, 4 };

            GenerateDataKeyResult generateDataKeyResult = new GenerateDataKeyResult
            {
                KeyPlaintext = dataKeyPlainText,
            };
            Mock<CryptoKey> generatedDataKeyCryptoKey = new Mock<CryptoKey>();
            string someKey = "some_key";
            awsKeyManagementServiceImplSpy
                .Setup(x =>
                    x.GenerateDataKey(
                        awsKeyManagementServiceImplSpy.Object.RegionToArnAndClientDictionary, out someKey))
                .Returns(generateDataKeyResult);
            cryptoMock.Setup(x => x.GenerateKeyFromBytes(generateDataKeyResult.KeyPlaintext))
                .Returns(generatedDataKeyCryptoKey.Object);

            // Inject unexpected exception so the Task.WaitAll throws an  exception
            awsKeyManagementServiceImplSpy
                .Setup(x => x.EncryptKeyAndBuildResult(
                    It.IsAny<IAmazonKeyManagementService>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>()))
                .Throws<SystemException>();
            cryptoMock.Setup(x => x.EncryptKey(cryptoKeyMock.Object, generatedDataKeyCryptoKey.Object))
                .Returns(encryptedKey);

            Assert.Throws<AppEncryptionException>(() =>
                awsKeyManagementServiceImplSpy.Object.EncryptKey(cryptoKeyMock.Object));
            Assert.Equal(new byte[] { 0, 0 }, dataKeyPlainText);
        }
    }
}
