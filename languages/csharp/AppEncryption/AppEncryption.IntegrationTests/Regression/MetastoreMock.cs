using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
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

         internal static Mock<IMetastorePersistence<JObject>> CreateMetastoreMock(
             AppEncryptionPartition appEncryptionPartition,
             KeyManagementService kms,
             KeyState metaIK,
             KeyState metaSK,
             CryptoKeyHolder cryptoKeyHolder,
             Type metaStoreType)
        {
            // TODO Change this to generate a mock dynamically based on the Metastore type
            CryptoKey systemKey = cryptoKeyHolder.SystemKey;

            Mock typeMock = (Mock)typeof(Mock<>).MakeGenericType(metaStoreType).GetConstructor(Type.EmptyTypes).Invoke(new object[] { });

            Mock<IMetastorePersistence<JObject>> metaStorePersistenceSpy = typeMock.As<IMetastorePersistence<JObject>>();
            metaStorePersistenceSpy.Setup(y => y.Store(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<JObject>()));
            metaStorePersistenceSpy.Setup(y => y.Load(It.IsAny<string>(),  It.IsAny<DateTimeOffset>()));
            metaStorePersistenceSpy.Setup(y => y.LoadLatestValue(It.IsAny<string>()));

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
