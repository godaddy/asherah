using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public static class MetastoreMock
    {
        private static readonly AeadEnvelopeCrypto Crypto = new BouncyAes256GcmCrypto();

        internal static Mock<IMetastorePersistence<JObject>> CreateMetastoreMock(
            AppEncryptionPartition appEncryptionPartition,
            KeyManagementService kms,
            KeyState metaIK,
            KeyState metaSK,
            CryptoKeyHolder cryptoKeyHolder,
            IMetastorePersistence<JObject> metaStore)
        {
            CryptoKey systemKey = cryptoKeyHolder.SystemKey;

            Mock<IMetastorePersistence<JObject>> metaStorePersistenceSpy = new Mock<IMetastorePersistence<JObject>>();

            metaStorePersistenceSpy.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .Returns<string, DateTimeOffset>(metaStore.Load);

            metaStorePersistenceSpy.Setup(x => x.LoadLatestValue(It.IsAny<string>()))
                .Returns<string>(metaStore.LoadLatestValue);

            metaStorePersistenceSpy.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()))
                .Returns<string, DateTimeOffset, JObject>(metaStore.Store);

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
                metaStorePersistenceSpy.Object.Store(
                    appEncryptionPartition.SystemKeyId,
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
                    new KeyMeta(appEncryptionPartition.SystemKeyId, systemKey.GetCreated()),
                    Crypto.EncryptKey(intermediateKey, systemKey),
                    intermediateKey.IsRevoked());
                metaStorePersistenceSpy.Object.Store(
                    appEncryptionPartition.IntermediateKeyId,
                    intermediateKeyRecord.Created,
                    intermediateKeyRecord.ToJson());
            }

            metaStorePersistenceSpy.Reset();
            return metaStorePersistenceSpy;
        }
    }
}
