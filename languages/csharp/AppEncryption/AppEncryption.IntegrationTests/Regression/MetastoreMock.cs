using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public static class MetastoreMock
    {
        private static readonly AeadEnvelopeCrypto Crypto = new BouncyAes256GcmCrypto();

        internal static Mock<IMetastore<JObject>> CreateMetastoreMock(
            Partition partition,
            KeyManagementService kms,
            KeyState metaIK,
            KeyState metaSK,
            CryptoKeyHolder cryptoKeyHolder,
            IMetastore<JObject> metastore)
        {
            CryptoKey systemKey = cryptoKeyHolder.SystemKey;

            Mock<IMetastore<JObject>> metastoreSpy = new Mock<IMetastore<JObject>>();

            metastoreSpy
                .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns<string, DateTimeOffset>(metastore.Load);
            metastoreSpy
                .Setup(x => x.LoadLatest(It.IsAny<string>()))
                .Returns<string>(metastore.LoadLatest);
            metastoreSpy
                .Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns<string, DateTimeOffset, JObject>(metastore.Store);

            if (metaSK != KeyState.Empty)
            {
                if (metaSK == KeyState.Retired)
                {
                    // We create a revoked copy of the same key
                    DateTimeOffset created = systemKey.GetCreated();
                    systemKey = systemKey
                        .WithKey(bytes => Crypto.GenerateKeyFromBytes(bytes, created, true));
                }

                EnvelopeKeyRecord systemKeyRecord = new EnvelopeKeyRecord(
                    systemKey.GetCreated(), null, kms.EncryptKey(systemKey), systemKey.IsRevoked());
                metastore.Store(
                    partition.SystemKeyId,
                    systemKeyRecord.Created,
                    systemKeyRecord.ToJson());
            }

            if (metaIK != KeyState.Empty)
            {
                CryptoKey intermediateKey = cryptoKeyHolder.IntermediateKey;
                if (metaIK == KeyState.Retired)
                {
                    // We create a revoked copy of the same key
                    DateTimeOffset created = intermediateKey.GetCreated();
                    intermediateKey = intermediateKey
                        .WithKey(bytes => Crypto.GenerateKeyFromBytes(bytes, created, true));
                }

                EnvelopeKeyRecord intermediateKeyRecord = new EnvelopeKeyRecord(
                    intermediateKey.GetCreated(),
                    new KeyMeta(partition.SystemKeyId, systemKey.GetCreated()),
                    Crypto.EncryptKey(intermediateKey, systemKey),
                    intermediateKey.IsRevoked());
                metastore.Store(
                    partition.IntermediateKeyId,
                    intermediateKeyRecord.Created,
                    intermediateKeyRecord.ToJson());
            }

            return metastoreSpy;
        }
    }
}
