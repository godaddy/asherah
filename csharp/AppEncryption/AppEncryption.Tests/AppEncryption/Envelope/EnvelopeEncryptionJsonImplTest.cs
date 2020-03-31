using System;
using System.Collections.Generic;
using System.Text;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using GoDaddy.Asherah.Crypto.Keys;
using LanguageExt;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Envelope
{
    [Collection("Logger Fixture collection")]
    public class EnvelopeEncryptionJsonImplTest : IClassFixture<MetricsFixture>
    {
        private readonly Partition partition =
            new Partition("shopper_123", "payments", "ecomm");

        // Setup DateTimeOffsets truncated to seconds and separated by hour to isolate overlap in case of interacting with multiple level keys
        private readonly DateTimeOffset drkDateTime = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromSeconds(1));
        private readonly DateTimeOffset ikDateTime;
        private readonly DateTimeOffset skDateTime;

        private readonly Mock<IMetastore<JObject>> metastoreMock;
        private readonly Mock<SecureCryptoKeyDictionary<DateTimeOffset>> systemKeyCacheMock;
        private readonly Mock<SecureCryptoKeyDictionary<DateTimeOffset>> intermediateKeyCacheMock;
        private readonly Mock<AeadEnvelopeCrypto> aeadEnvelopeCryptoMock;
        private readonly Mock<CryptoPolicy> cryptoPolicyMock;
        private readonly Mock<KeyManagementService> keyManagementServiceMock;

        // Convenience mocks
        private readonly Mock<CryptoKey> intermediateCryptoKeyMock;
        private readonly Mock<CryptoKey> systemCryptoKeyMock;
        private readonly Mock<KeyMeta> keyMetaMock;

        private readonly Mock<EnvelopeEncryptionJsonImpl> envelopeEncryptionJsonImplSpy;

        public EnvelopeEncryptionJsonImplTest()
        {
            // Have to init these in constructor
            ikDateTime = drkDateTime.AddHours(-1);
            skDateTime = ikDateTime.AddHours(-1);

            metastoreMock = new Mock<IMetastore<JObject>>();
            systemKeyCacheMock = new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(1000);
            intermediateKeyCacheMock = new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(1000);
            aeadEnvelopeCryptoMock = new Mock<AeadEnvelopeCrypto>();
            cryptoPolicyMock = new Mock<CryptoPolicy>();
            keyManagementServiceMock = new Mock<KeyManagementService>();

            intermediateCryptoKeyMock = new Mock<CryptoKey>();
            systemCryptoKeyMock = new Mock<CryptoKey>();
            keyMetaMock = new Mock<KeyMeta>("some_keyid", DateTimeOffset.UtcNow);

            envelopeEncryptionJsonImplSpy = new Mock<EnvelopeEncryptionJsonImpl>(
                partition,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                intermediateKeyCacheMock.Object,
                aeadEnvelopeCryptoMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object) { CallBase = true };
        }

        [Fact]
        private void TestDecryptDataRowRecordWithParentKeyMetaShouldSucceed()
        {
            KeyMeta intermediateKeyMeta = new KeyMeta("parentKeyId", ikDateTime);
            EnvelopeKeyRecord dataRowKey = new EnvelopeKeyRecord(drkDateTime, intermediateKeyMeta, new byte[] { 0, 1, 2, 3 });
            byte[] encryptedData = { 4, 5, 6, 7 };

            JObject dataRowRecord = JObject.FromObject(new Dictionary<string, object>
            {
                { "Key", dataRowKey.ToJson() },
                { "Data", Convert.ToBase64String(encryptedData) },
            });

            envelopeEncryptionJsonImplSpy.Setup(x => x.WithIntermediateKeyForRead(intermediateKeyMeta, It.IsAny<Func<CryptoKey, byte[]>>()))
                .Returns<KeyMeta, Func<CryptoKey, byte[]>>((keyMeta, functionWithIntermediateKey) => functionWithIntermediateKey(intermediateCryptoKeyMock.Object));

            byte[] expectedDecryptedPayload = { 11, 12, 13, 14 };
            aeadEnvelopeCryptoMock
                .Setup(x => x.EnvelopeDecrypt(
                    encryptedData, dataRowKey.EncryptedKey, dataRowKey.Created, intermediateCryptoKeyMock.Object))
                .Returns(expectedDecryptedPayload);

            byte[] actualDecryptedPayload = envelopeEncryptionJsonImplSpy.Object.DecryptDataRowRecord(dataRowRecord);
            Assert.Equal(expectedDecryptedPayload, actualDecryptedPayload);
            aeadEnvelopeCryptoMock.Verify(x => x.EnvelopeDecrypt(encryptedData, dataRowKey.EncryptedKey, dataRowKey.Created, intermediateCryptoKeyMock.Object));
        }

        [Fact]
        private void TestDecryptDataRowRecordWithoutParentKeyMetaShouldFail()
        {
            EnvelopeKeyRecord dataRowKey = new EnvelopeKeyRecord(drkDateTime, null, new byte[] { 0, 1, 2, 3 });
            byte[] encryptedData = { 4, 5, 6, 7 };

            JObject dataRowRecord = JObject.FromObject(new Dictionary<string, object>
            {
                { "Key", dataRowKey.ToJson() },
                { "Data", Convert.ToBase64String(encryptedData) },
            });

            Assert.Throws<MetadataMissingException>(() =>
                envelopeEncryptionJsonImplSpy.Object.DecryptDataRowRecord(dataRowRecord));
        }

        [Fact]
        private void TestEncryptPayload()
        {
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);

            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithIntermediateKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeEncryptResult>>()))
                .Returns<Func<CryptoKey, EnvelopeEncryptResult>>(functionWithIntermediateKey =>
                    functionWithIntermediateKey(intermediateCryptoKeyMock.Object));

            byte[] decryptedPayload = Encoding.Unicode.GetBytes("somepayload");
            KeyMeta intermediateKeyMeta = new KeyMeta(partition.IntermediateKeyId, ikDateTime);
            byte[] encryptedPayload = { 0, 1, 2, 3 };
            byte[] encryptedKey = { 4, 5, 6, 7 };
            EnvelopeEncryptResult envelopeEncryptResult = new EnvelopeEncryptResult
            {
                CipherText = encryptedPayload,
                EncryptedKey = encryptedKey,
                UserState = intermediateKeyMeta,
            };
            aeadEnvelopeCryptoMock
                .Setup(x => x.EnvelopeEncrypt(decryptedPayload, intermediateCryptoKeyMock.Object, intermediateKeyMeta))
                .Returns(envelopeEncryptResult);

            EnvelopeKeyRecord expectedDataRowKey = new EnvelopeKeyRecord(drkDateTime, intermediateKeyMeta, encryptedKey);
            JObject expectedDataRowRecord = JObject.FromObject(new Dictionary<string, object>
            {
                { "Key", expectedDataRowKey.ToJson() },
                { "Data", Convert.ToBase64String(encryptedPayload) },
            });

            JObject actualDataRowRecord = envelopeEncryptionJsonImplSpy.Object.EncryptPayload(decryptedPayload);

            // Asserting individual fields as work-around to hard-coding DateTimeOffset.UtcNow usage
            Assert.Equal(expectedDataRowRecord.GetValue("Data").ToObject<string>(), actualDataRowRecord.GetValue("Data").ToObject<string>());
            Assert.Equal(
                expectedDataRowRecord.GetValue("Key").ToObject<JObject>().GetValue("Key").ToString(),
                actualDataRowRecord.GetValue("Key").ToObject<JObject>().GetValue("Key").ToString());
            Assert.True(JToken.DeepEquals(
                expectedDataRowRecord.GetValue("Key").ToObject<JObject>().GetValue("ParentKeyMeta").ToObject<JObject>(),
                actualDataRowRecord.GetValue("Key").ToObject<JObject>().GetValue("ParentKeyMeta").ToObject<JObject>()));
        }

        [Fact]
        private void TestDisposeSuccess()
        {
            envelopeEncryptionJsonImplSpy.Object.Dispose();

            // Verify proper resources are disposed
            intermediateKeyCacheMock.Verify(x => x.Dispose());
            systemKeyCacheMock.Verify(x => x.Dispose(), Times.Never); // Shouldn't be disposed
        }

        [Fact]
        private void TestDisposeWithDisposeFailShouldReturn()
        {
            intermediateKeyCacheMock.Setup(x => x.Dispose()).Throws(new SystemException());
            envelopeEncryptionJsonImplSpy.Object.Dispose();

            intermediateKeyCacheMock.Verify(x => x.Dispose());
            systemKeyCacheMock.Verify(x => x.Dispose(), Times.Never); // Shouldn't be disposed
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyCachedAndNotExpiredShouldUseCache()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            intermediateKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).
                Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(It.IsAny<DateTimeOffset>()), Times.Never);
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyCachedAndNotExpiredAndNotifyExpiredShouldUseCacheAndNotNotify()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            intermediateKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created))
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.NotifyExpiredIntermediateKeyOnRead()).Returns(true);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(It.IsAny<DateTimeOffset>()), Times.Never);

            // TODO : Add verify for notification not being called once implemented
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyCachedAndExpiredAndNotifyExpiredShouldUseCacheAndNotify()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            intermediateKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created))
                .Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(intermediateCryptoKeyMock.Object))
                .Returns(true);
            cryptoPolicyMock.Setup(x => x.NotifyExpiredIntermediateKeyOnRead()).Returns(true);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(It.IsAny<DateTimeOffset>()), Times.Never);

            // TODO : Add verify for notification not being called once implemented
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyNotCachedAndCannotCacheAndNotExpiredShouldLookup()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetIntermediateKey(It.IsAny<DateTimeOffset>()))
                .Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(keyMetaMock.Object.Created));
            intermediateKeyCacheMock.Verify(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()), Times.Never);
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyNotCachedAndCanCacheAndNotExpiredShouldLookupAndCache()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetIntermediateKey(ikDateTime))
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheIntermediateKeys()).Returns(true);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            intermediateKeyCacheMock
                .Setup(x => x.PutAndGetUsable(intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object))
                .Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(keyMetaMock.Object.Created));
            intermediateKeyCacheMock.Verify(x => x.PutAndGetUsable(intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object));
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForReadWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndDisposeKey()
        {
            keyMetaMock.Setup(x => x.Created).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetIntermediateKey(It.IsAny<DateTimeOffset>()))
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheIntermediateKeys()).Returns(true);
            intermediateKeyCacheMock.Setup(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()))
                .Throws(new AppEncryptionException("fake exception"));

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            Assert.Throws<AppEncryptionException>(() => envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForRead(
                keyMetaMock.Object, functionWithIntermediateKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetIntermediateKey(keyMetaMock.Object.Created));
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForWriteWithKeyNotCachedAndCannotCacheShouldLookup()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateIntermediateKey())
                .Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForWrite(functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateIntermediateKey());
            intermediateKeyCacheMock.Verify(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()), Times.Never);
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForWriteWithKeyNotCachedAndCanCacheShouldLookupAndCache()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateIntermediateKey())
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheIntermediateKeys()).Returns(true);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            intermediateKeyCacheMock
                .Setup(x => x.PutAndGetUsable(
                    intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object)).
                Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForWrite(functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateIntermediateKey());
            intermediateKeyCacheMock.Verify(x => x.PutAndGetUsable(intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object));
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForWriteWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndDisposeKey()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateIntermediateKey())
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheIntermediateKeys()).Returns(true);
            intermediateKeyCacheMock.Setup(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()))
                .Throws(new AppEncryptionException("fake exception"));

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForWrite(functionWithIntermediateKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateIntermediateKey());
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForWriteWithKeyCachedAndExpiredShouldLookup()
        {
            Mock<CryptoKey> expiredCryptoKeyMock = new Mock<CryptoKey>();
            intermediateKeyCacheMock.Setup(x => x.GetLast()).Returns(expiredCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(expiredCryptoKeyMock.Object))
                .Returns(true);
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateIntermediateKey())
                .Returns(intermediateCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheIntermediateKeys()).Returns(true);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            intermediateKeyCacheMock
                .Setup(x => x.PutAndGetUsable(intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object))
                .Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForWrite(functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);

            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateIntermediateKey());
            intermediateKeyCacheMock.Verify(x => x.PutAndGetUsable(intermediateCryptoKeyMock.Object.GetCreated(), intermediateCryptoKeyMock.Object));
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithIntermediateKeyForWriteWithKeyCachedAndNotExpiredShouldUseCache()
        {
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            intermediateKeyCacheMock.Setup(x => x.GetLast()).Returns(intermediateCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithIntermediateKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithIntermediateKeyForWrite(functionWithIntermediateKey);
            Assert.Equal(expectedBytes, actualBytes);

            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndNotExpiredShouldUseCache()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes =
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndExpiredAndNotTreatAsMissingAndNotNotifyExpiredShouldUseCacheAndNotNotify()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object)).Returns(true);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes =
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);

            // TODO : Add verify for notification not being called once implemented
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndExpiredAndNotTreatAsMissingAndNotifyExpiredShouldUseCacheAndNotify()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object)).Returns(true);
            cryptoPolicyMock.Setup(x => x.NotifyExpiredSystemKeyOnRead()).Returns(true);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes =
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);

            // TODO : Add verify for notification not being called once implemented
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndExpiredAndTreatAsMissingShouldThrowMetadataMissingException()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object)).Returns(true);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            Assert.Throws<MetadataMissingException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, true, functionWithSystemKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyNotCachedAndCannotCacheAndNotExpiredShouldLookup()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetSystemKey(It.IsAny<KeyMeta>()))
                .Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes =
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(keyMetaMock.Object));
            systemKeyCacheMock.Verify(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyNotCachedAndCanCacheAndNotExpiredShouldLookupAndCache()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetSystemKey(It.IsAny<KeyMeta>()))
                .Returns(systemCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheSystemKeys()).Returns(true);
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.PutAndGetUsable(keyMetaMock.Object.Created, systemCryptoKeyMock.Object))
                .Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes =
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(keyMetaMock.Object));
            systemKeyCacheMock.Verify(x => x.PutAndGetUsable(keyMetaMock.Object.Created, systemCryptoKeyMock.Object));
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndDisposeKey()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetSystemKey(It.IsAny<KeyMeta>()))
                .Returns(systemCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheSystemKeys()).Returns(true);
            systemKeyCacheMock.Setup(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()))
                .Throws(new AppEncryptionException("fake exception"));

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(keyMetaMock.Object));
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndNotExpiredAndFunctionThrowsErrorShouldThrowErrorAndDisposeKey()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);

            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => throw new AppEncryptionException("fake error");

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithExistingSystemKeyWithKeyCachedAndNotExpiredAndDisposeKeyThrowsErrorShouldThrowError()
        {
            keyMetaMock.Setup(x => x.Created).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.Get(keyMetaMock.Object.Created)).Returns(systemCryptoKeyMock.Object);
            systemCryptoKeyMock.Setup(x => x.Dispose()).Throws(new AppEncryptionException("fake error"));

            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => new byte[] { };

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithExistingSystemKey(keyMetaMock.Object, false, functionWithSystemKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetSystemKey(It.IsAny<KeyMeta>()), Times.Never);
        }

        [Fact]
        private void TestWithSystemKeyForWriteWithKeyNotCachedAndCannotCacheShouldLookup()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateSystemKey())
                .Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithSystemKeyForWrite(functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateSystemKey());
            systemKeyCacheMock.Verify(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithSystemKeyForWriteWithKeyNotCachedAndCanCacheShouldLookupAndCache()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateSystemKey())
                .Returns(systemCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheSystemKeys()).Returns(true);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            systemKeyCacheMock
                .Setup(x => x.PutAndGetUsable(systemCryptoKeyMock.Object.GetCreated(), systemCryptoKeyMock.Object))
                .Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithSystemKeyForWrite(functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateSystemKey());
            systemKeyCacheMock.Verify(x => x.PutAndGetUsable(systemCryptoKeyMock.Object.GetCreated(), systemCryptoKeyMock.Object));
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithSystemKeyForWriteWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndDisposeKey()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateSystemKey())
                .Returns(systemCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheSystemKeys()).Returns(true);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.PutAndGetUsable(It.IsAny<DateTimeOffset>(), It.IsAny<CryptoKey>()))
                .Throws(new AppEncryptionException("fake exception"));

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.WithSystemKeyForWrite(functionWithSystemKey));
            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateSystemKey());
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithSystemKeyForWriteWithKeyCachedAndExpiredShouldLookup()
        {
            Mock<CryptoKey> expiredCryptoKey = new Mock<CryptoKey>();
            systemKeyCacheMock.Setup(x => x.GetLast()).Returns(expiredCryptoKey.Object);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(expiredCryptoKey.Object)).Returns(true);
            envelopeEncryptionJsonImplSpy.Setup(x => x.GetLatestOrCreateSystemKey())
                .Returns(systemCryptoKeyMock.Object);
            cryptoPolicyMock.Setup(x => x.CanCacheSystemKeys()).Returns(true);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            systemKeyCacheMock
                .Setup(x => x.PutAndGetUsable(systemCryptoKeyMock.Object.GetCreated(), systemCryptoKeyMock.Object))
                .Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithSystemKeyForWrite(functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);

            envelopeEncryptionJsonImplSpy.Verify(x => x.GetLatestOrCreateSystemKey());
            systemKeyCacheMock.Verify(x => x.PutAndGetUsable(systemCryptoKeyMock.Object.GetCreated(), systemCryptoKeyMock.Object));
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestWithSystemKeyForWriteWithKeyCachedAndNotExpiredShouldUseCache()
        {
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            systemKeyCacheMock.Setup(x => x.GetLast()).Returns(systemCryptoKeyMock.Object);

            byte[] expectedBytes = { 0, 1, 2, 3 };
            Func<CryptoKey, byte[]> functionWithSystemKey = cryptoKey => expectedBytes;

            byte[] actualBytes = envelopeEncryptionJsonImplSpy.Object.WithSystemKeyForWrite(functionWithSystemKey);
            Assert.Equal(expectedBytes, actualBytes);

            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithEmptyMetastoreShouldCreateSuccessfully()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithEmptyMetastoreShouldAttemptCreateAndFailAndDisposeKey()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Throws(new AppEncryptionException("fake error"));

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey());
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            intermediateCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndInlineRotationShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
               ikDateTime.AddSeconds(-1),
               new KeyMeta("id", skDateTime.AddSeconds(-1)),
               new byte[] { 0, 1, 2, 3 },
               false);

            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(It.IsAny<EnvelopeKeyRecord>()))
                .Returns(true);
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredShouldUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("id", skDateTime),
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));

            envelopeEncryptionJsonImplSpy.Setup(x =>
                    x.WithExistingSystemKey((KeyMeta)keyRecord.ParentKeyMeta, true, It.IsAny<Func<CryptoKey, CryptoKey>>()))
                .Returns<KeyMeta, bool, Func<CryptoKey, CryptoKey>>(
                    (keyMeta, treatExpiredAsMissing, functionWithSystemKey) =>
                        functionWithSystemKey(systemCryptoKeyMock.Object));
            envelopeEncryptionJsonImplSpy.Setup(x => x.DecryptKey(keyRecord, systemCryptoKeyMock.Object)).Returns(intermediateCryptoKeyMock.Object);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredAndNoParentKeyMetaShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime, null, new byte[] { 0, 1, 2, 3 }, false);

            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredAndWithExistingSystemKeyFailsShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
               ikDateTime.AddSeconds(-1),
               new KeyMeta("id", skDateTime.AddSeconds(-1)),
               new byte[] { 0, 1, 2, 3 },
               false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithExistingSystemKey(It.IsAny<KeyMeta>(), It.IsAny<bool>(), It.IsAny<Func<CryptoKey, byte[]>>()))
                .Throws(new MetadataMissingException("fake error"));
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationShouldQueueAndUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("id", skDateTime),
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(It.IsAny<EnvelopeKeyRecord>()))
                .Returns(true);
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).Returns(true);

            envelopeEncryptionJsonImplSpy.Setup(x =>
                    x.WithExistingSystemKey((KeyMeta)keyRecord.ParentKeyMeta, true, It.IsAny<Func<CryptoKey, CryptoKey>>()))
                .Returns<KeyMeta, bool, Func<CryptoKey, CryptoKey>>(
                    (keyMeta, treatExpiredAsMissing, functionWithSystemKey) =>
                        functionWithSystemKey(systemCryptoKeyMock.Object));
            envelopeEncryptionJsonImplSpy.Setup(x => x.DecryptKey(keyRecord, systemCryptoKeyMock.Object)).Returns(intermediateCryptoKeyMock.Object);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);

            // TODO : Add verify for queue key rotation once implemented
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationAndNoParentKeyMetaShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime, null, new byte[] { 0, 1, 2, 3 }, false);

            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(It.IsAny<EnvelopeKeyRecord>()))
                .Returns(true);
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).Returns(true);
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);

            // TODO : Add verify for queue key rotation once implemented
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationAndWithExistingSystemKeyFailsShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime.AddSeconds(-1),
                new KeyMeta("id", skDateTime.AddSeconds(-1)),
                new byte[] { 0, 1, 2, 3 },
                false);

            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(It.IsAny<EnvelopeKeyRecord>()))
                .Returns(true);
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).Returns(true);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithExistingSystemKey(It.IsAny<KeyMeta>(), It.IsAny<bool>(), It.IsAny<Func<CryptoKey, byte[]>>()))
                .Throws(new MetadataMissingException("fake error"));
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            intermediateCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(intermediateCryptoKeyMock.Object);
            envelopeEncryptionJsonImplSpy
                .Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);

            // TODO : Add verify for queue key rotation once implemented
            aeadEnvelopeCryptoMock.Verify(x => x.EncryptKey(intermediateCryptoKeyMock.Object, systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.IntermediateKeyId, ikDateTime, It.IsAny<JObject>()));
            intermediateCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptShouldRetryOnce()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime.AddSeconds(-1),
                new KeyMeta("id", skDateTime.AddSeconds(-1)),
                new byte[] { 0, 1, 2, 3 },
                false);
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.SetupSequence(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None)
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(unusedCryptoKeyMock.Object);
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x =>
                    x.WithExistingSystemKey((KeyMeta)keyRecord.ParentKeyMeta, true, It.IsAny<Func<CryptoKey, CryptoKey>>()))
                .Returns<KeyMeta, bool, Func<CryptoKey, CryptoKey>>(
                    (keyMeta, treatExpiredAsMissing, functionWithSystemKey) =>
                        functionWithSystemKey(systemCryptoKeyMock.Object));
            envelopeEncryptionJsonImplSpy.Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.DecryptKey(keyRecord, systemCryptoKeyMock.Object))
                .Returns(intermediateCryptoKeyMock.Object);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey();
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptAndNoParentKeyMetaShouldFail()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime.AddSeconds(-1),
                null,
                new byte[] { 0, 1, 2, 3 },
                false);
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.SetupSequence(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None)
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(unusedCryptoKeyMock.Object);
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);

            Assert.Throws<MetadataMissingException>(() =>
                envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey());
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptShouldRetryOnceButSecondTimeFails()
        {
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);
            cryptoPolicyMock.Setup(x => x.TruncateToIntermediateKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(ikDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(ikDateTime)).Returns(unusedCryptoKeyMock.Object);
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(ikDateTime);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            envelopeEncryptionJsonImplSpy.Setup(x => x.WithSystemKeyForWrite(It.IsAny<Func<CryptoKey, EnvelopeKeyRecord>>()))
                .Returns<Func<CryptoKey, EnvelopeKeyRecord>>(functionWithDecryptedSystemKey =>
                    functionWithDecryptedSystemKey(systemCryptoKeyMock.Object));
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);
            aeadEnvelopeCryptoMock.Setup(x => x.EncryptKey(It.IsAny<CryptoKey>(), It.IsAny<CryptoKey>()))
                .Returns(new byte[] { 0, 1, 2, 3 });

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateIntermediateKey());
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithEmptyMetastoreShouldCreateSuccessfully()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);
            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(systemCryptoKeyMock.Object);
            keyManagementServiceMock.Setup(x => x.EncryptKey(systemCryptoKeyMock.Object))
                .Returns(new byte[] { 0, 1, 2, 3 });
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            keyManagementServiceMock.Verify(x => x.EncryptKey(systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.SystemKeyId, skDateTime, It.IsAny<JObject>()));
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithEmptyMetastoreShouldAttemptCreateAndFailAndDisposeKey()
        {
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);

            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(systemCryptoKeyMock.Object);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            keyManagementServiceMock.Setup(x => x.EncryptKey(systemCryptoKeyMock.Object))
                .Returns(new byte[] { 0, 1, 2, 3 });
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Throws(new AppEncryptionException("fake error"));

            Assert.Throws<AppEncryptionException>(() => envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey());
            envelopeEncryptionJsonImplSpy.Verify(x => x.DecryptKey(It.IsAny<EnvelopeKeyRecord>(), It.IsAny<CryptoKey>()), Times.Never);
            keyManagementServiceMock.Verify(x => x.EncryptKey(systemCryptoKeyMock.Object));
            systemCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithLatestAndExpiredAndInlineRotationShouldCreateSuccessfully()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime.AddSeconds(-1),
                null,
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(It.IsAny<EnvelopeKeyRecord>()))
                .Returns(true);

            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(systemCryptoKeyMock.Object);
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            keyManagementServiceMock.Setup(x => x.EncryptKey(systemCryptoKeyMock.Object))
                .Returns(new byte[] { 0, 1, 2, 3 });
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(true);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);
            keyManagementServiceMock.Verify(x => x.DecryptKey(It.IsAny<byte[]>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>()), Times.Never);
            keyManagementServiceMock.Verify(x => x.EncryptKey(systemCryptoKeyMock.Object));
            metastoreMock.Verify(x => x.Store(partition.SystemKeyId, skDateTime, It.IsAny<JObject>()));
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithLatestAndNotExpiredAndNonNullRevokedShouldUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(keyRecord)).Returns(false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, (bool)keyRecord.Revoked))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithLatestAndNotExpiredAndNullRevokedShouldUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                null);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, false))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithLatestAndExpiredAndQueuedRotationAndNonNullRevokedShouldQueueAndUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                true);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(keyRecord)).Returns(true);
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).Returns(true);
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, (bool)keyRecord.Revoked))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);

            // TODO : Add verify for queue key rotation once implemented
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithLatestAndExpiredAndQueuedRotationAndNullRevokedShouldQueueAndUseLatest()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                null);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));
            envelopeEncryptionJsonImplSpy.Setup(x => x.IsKeyExpiredOrRevoked(keyRecord)).Returns(true);
            cryptoPolicyMock.Setup(x => x.IsQueuedKeyRotation()).Returns(true);
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, false))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey actualSystemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, actualSystemKey);

            // TODO : Add verify for queue key rotation once implemented
            metastoreMock.Verify(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()), Times.Never);
            systemCryptoKeyMock.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptAndNonNullRevokedShouldRetryOnce()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime.AddSeconds(-1),
                null,
                new byte[] { 0, 1, 2, 3 },
                true);
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.SetupSequence(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None)
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));

            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(unusedCryptoKeyMock.Object);
            keyManagementServiceMock.Setup(x => x.EncryptKey(unusedCryptoKeyMock.Object)).Returns(new byte[] { 0, 1, 2, 3 });
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, (bool)keyRecord.Revoked))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey systemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, systemKey);
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptAndNullRevokedShouldRetryOnce()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime.AddSeconds(-1),
                null,
                new byte[] { 0, 1, 2, 3 },
                null);
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.SetupSequence(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None)
                .Returns(Option<EnvelopeKeyRecord>.Some(keyRecord));

            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(unusedCryptoKeyMock.Object);
            keyManagementServiceMock.Setup(x => x.EncryptKey(unusedCryptoKeyMock.Object)).Returns(new byte[] { 0, 1, 2, 3 });
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);
            keyManagementServiceMock
                .Setup(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, false))
                .Returns(systemCryptoKeyMock.Object);

            CryptoKey systemKey = envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey();
            Assert.Equal(systemCryptoKeyMock.Object, systemKey);
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptShouldRetryOnceButSecondTimeFailsThrowsException()
        {
            Mock<CryptoKey> unusedCryptoKeyMock = new Mock<CryptoKey>();

            envelopeEncryptionJsonImplSpy.SetupSequence(x => x.LoadLatestKeyRecord(It.IsAny<string>()))
                .Returns(Option<EnvelopeKeyRecord>.None);

            cryptoPolicyMock.Setup(x => x.TruncateToSystemKeyPrecision(It.IsAny<DateTimeOffset>())).Returns(skDateTime);
            aeadEnvelopeCryptoMock.Setup(x => x.GenerateKey(skDateTime)).Returns(unusedCryptoKeyMock.Object);
            keyManagementServiceMock.Setup(x => x.EncryptKey(unusedCryptoKeyMock.Object)).Returns(new byte[] { 0, 1, 2, 3 });
            unusedCryptoKeyMock.Setup(x => x.GetCreated()).Returns(skDateTime);
            metastoreMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns(false);

            Assert.Throws<AppEncryptionException>(() =>
                envelopeEncryptionJsonImplSpy.Object.GetLatestOrCreateSystemKey());
            unusedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestGetIntermediateKeyWithParentKeyMetaShouldSucceed()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("id", skDateTime),
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadKeyRecord(It.IsAny<string>(), ikDateTime))
                .Returns(keyRecord);
            envelopeEncryptionJsonImplSpy.Setup(x =>
                    x.WithExistingSystemKey((KeyMeta)keyRecord.ParentKeyMeta, false, It.IsAny<Func<CryptoKey, CryptoKey>>()))
                .Returns<KeyMeta, bool, Func<CryptoKey, CryptoKey>>(
                    (keyMeta, treatExpiredAsMissing, functionWithSystemKey) => functionWithSystemKey(systemCryptoKeyMock.Object));
            envelopeEncryptionJsonImplSpy.Setup(x => x.DecryptKey(keyRecord, systemCryptoKeyMock.Object))
                .Returns(intermediateCryptoKeyMock.Object);

            CryptoKey actualIntermediateKey = envelopeEncryptionJsonImplSpy.Object.GetIntermediateKey(ikDateTime);
            Assert.Equal(intermediateCryptoKeyMock.Object, actualIntermediateKey);
            envelopeEncryptionJsonImplSpy.Verify(x => x.WithExistingSystemKey(
                (KeyMeta)keyRecord.ParentKeyMeta, false, It.IsAny<Func<CryptoKey, CryptoKey>>()));
        }

        [Fact]
        private void TestGetIntermediateKeyWithoutParentKeyMetaShouldFail()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                false);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadKeyRecord(It.IsAny<string>(), ikDateTime))
                .Returns(keyRecord);

            Assert.Throws<MetadataMissingException>(() => envelopeEncryptionJsonImplSpy.Object.GetIntermediateKey(ikDateTime));
        }

        [Fact]
        private void TestGetSystemKeyWithNonNullRevoked()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                true);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadKeyRecord(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns(keyRecord);

            envelopeEncryptionJsonImplSpy.Object.GetSystemKey(keyMetaMock.Object);
            keyManagementServiceMock.Verify(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, (bool)keyRecord.Revoked));
        }

        [Fact]
        private void TestGetSystemKeyWithNullRevoked()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                skDateTime,
                null,
                new byte[] { 0, 1, 2, 3 },
                null);
            envelopeEncryptionJsonImplSpy.Setup(x => x.LoadKeyRecord(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns(keyRecord);

            envelopeEncryptionJsonImplSpy.Object.GetSystemKey(keyMetaMock.Object);
            keyManagementServiceMock.Verify(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, false));
        }

        [Fact]
        private void TestDecryptKeyWithNonNullRevoked()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("id", skDateTime),
                new byte[] { 0, 1, 2, 3 },
                true);

            envelopeEncryptionJsonImplSpy.Object.DecryptKey(keyRecord, intermediateCryptoKeyMock.Object);
            aeadEnvelopeCryptoMock.Verify(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, intermediateCryptoKeyMock.Object, true));
        }

        [Fact]
        private void TestDecryptKeyWithNullRevoked()
        {
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("id", skDateTime),
                new byte[] { 0, 1, 2, 3 },
                null);

            envelopeEncryptionJsonImplSpy.Object.DecryptKey(keyRecord, intermediateCryptoKeyMock.Object);
            aeadEnvelopeCryptoMock.Verify(x => x.DecryptKey(keyRecord.EncryptedKey, keyRecord.Created, intermediateCryptoKeyMock.Object, false));
        }

        [Fact]
        private void TestLoadKeyRecord()
        {
            byte[] pretendKeyBytes = { 0, 1, 2, 3, 4, 5, 6, 7 };
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("KeyId", skDateTime),
                pretendKeyBytes,
                false);

            metastoreMock.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns(Option<JObject>.Some(keyRecord.ToJson()));

            EnvelopeKeyRecord returnedEnvelopeKeyRecord = envelopeEncryptionJsonImplSpy.Object.LoadKeyRecord("empty", ikDateTime);
            Assert.Equal(keyRecord.Created, returnedEnvelopeKeyRecord.Created);
            Assert.Equal(keyRecord.ParentKeyMeta, returnedEnvelopeKeyRecord.ParentKeyMeta);
            Assert.Equal(keyRecord.EncryptedKey, returnedEnvelopeKeyRecord.EncryptedKey);
        }

        [Fact]
        private void TestLoadKeyRecordMissingItem()
        {
            metastoreMock.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns(Option<JObject>.None);

            Assert.Throws<MetadataMissingException>(() =>
                envelopeEncryptionJsonImplSpy.Object.LoadKeyRecord("empty", DateTimeOffset.UtcNow));
        }

        [Fact]
        private void TestLoadLatestKeyRecord()
        {
            byte[] pretendKeyBytes = { 0, 1, 2, 3, 4, 5, 6, 7 };
            EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(
                ikDateTime,
                new KeyMeta("KeyId", skDateTime),
                pretendKeyBytes,
                false);

            metastoreMock.Setup(x => x.LoadLatest(It.IsAny<string>()))
                .Returns(Option<JObject>.Some(keyRecord.ToJson()));

            Option<EnvelopeKeyRecord> returnedOptionalEnvelopeKeyRecord = envelopeEncryptionJsonImplSpy.Object.LoadLatestKeyRecord("empty");
            Assert.True(returnedOptionalEnvelopeKeyRecord.IsSome);
            Assert.Equal(ikDateTime, ((EnvelopeKeyRecord)returnedOptionalEnvelopeKeyRecord).Created);
            Assert.Equal(keyRecord.ParentKeyMeta, ((EnvelopeKeyRecord)returnedOptionalEnvelopeKeyRecord).ParentKeyMeta);
            Assert.Equal(keyRecord.EncryptedKey, ((EnvelopeKeyRecord)returnedOptionalEnvelopeKeyRecord).EncryptedKey);
        }

        [Fact]
        private void TestLoadLatestKeyRecordEmptyResult()
        {
            metastoreMock.Setup(x => x.LoadLatest(It.IsAny<string>())).Returns(Option<JObject>.None);

            Assert.False(envelopeEncryptionJsonImplSpy.Object.LoadLatestKeyRecord("empty").IsSome);
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedEnvelopeKeyRecordWithExpired()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(now, null, new byte[] { 0, 1 });
            cryptoPolicyMock.Setup(x => x.IsKeyExpired(now)).Returns(true);

            Assert.True(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(record));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndRevoked()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(
                DateTimeOffset.UtcNow,
                null,
                new byte[] { 0, 1 },
                true);

            Assert.True(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(record));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndNotRevoked()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(
                DateTimeOffset.UtcNow,
                null,
                new byte[] { 0, 1 });

            Assert.False(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(record));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndNullRevokedShouldDefaultToFalse()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(
                DateTimeOffset.UtcNow,
                null,
                new byte[] { 0, 1 },
                null);

            Assert.False(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(record));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedCryptoKeyWithExpired()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            systemCryptoKeyMock.Setup(x => x.GetCreated()).Returns(now);
            cryptoPolicyMock.Setup(x => x.IsKeyExpired(now)).Returns(true);

            Assert.True(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedCryptoKeyWithNotExpiredAndRevoked()
        {
            systemCryptoKeyMock.Setup(x => x.IsRevoked()).Returns(true);

            Assert.True(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object));
        }

        [Fact]
        private void TestIsKeyExpiredOrRevokedCryptoKeyWithNotExpiredAndNotRevoked()
        {
            Assert.False(envelopeEncryptionJsonImplSpy.Object.IsKeyExpiredOrRevoked(systemCryptoKeyMock.Object));
        }
    }
}
