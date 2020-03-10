using System;
using System.Collections.Generic;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Keys;
using LanguageExt;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Json
{
    [Collection("Logger Fixture collection")]
    public class AppJsonEncryptionImplTest : IClassFixture<MetricsFixture>
    {
        private readonly IMetastore<JObject> metastore;
        private readonly Persistence<JObject> dataPersistence;
        private readonly Partition partition;
        private readonly KeyManagementService keyManagementService;

        public AppJsonEncryptionImplTest()
        {
            partition = new Partition("PARTITION", "SYSTEM", "PRODUCT");
            Dictionary<string, JObject> memoryPersistence = new Dictionary<string, JObject>();

            dataPersistence = new AdhocPersistence<JObject>(
                key => memoryPersistence.TryGetValue(key, out JObject result) ? result : Option<JObject>.None,
                (key, jsonObject) => memoryPersistence.Add(key, jsonObject));

            metastore = new InMemoryMetastoreImpl<JObject>();
            keyManagementService = new DummyKeyManagementService();

            AeadEnvelopeCrypto aeadEnvelopeCrypto = new BouncyAes256GcmCrypto();

            // Generate a dummy systemKey document
            CryptoKey systemKey = aeadEnvelopeCrypto.GenerateKey();
            byte[] encryptedSystemKey = keyManagementService.EncryptKey(systemKey);

            EnvelopeKeyRecord systemKeyRecord = new EnvelopeKeyRecord(DateTimeOffset.UtcNow, null, encryptedSystemKey);

            // Write out the dummy systemKey record
            memoryPersistence.TryAdd(partition.SystemKeyId, systemKeyRecord.ToJson());
        }

        [Theory]
        [InlineData("GoDaddy")]
        [InlineData("ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ")]
        [InlineData(
            "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘")]
        public void TestRoundTrip(string testData)
        {
            RoundTripGeneric(testData, new BouncyAes256GcmCrypto());
        }

        private void RoundTripGeneric(string testData, AeadEnvelopeCrypto aeadEnvelopeCrypto)
        {
            CryptoPolicy cryptoPolicy = new DummyCryptoPolicy();
            using (SecureCryptoKeyDictionary<DateTimeOffset> secureCryptoKeyDictionary =
                new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis()))
            {
                IEnvelopeEncryption<JObject> envelopeEncryptionJsonImpl = new EnvelopeEncryptionJsonImpl(
                    partition,
                    metastore,
                    secureCryptoKeyDictionary,
                    new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis()),
                    aeadEnvelopeCrypto,
                    cryptoPolicy,
                    keyManagementService);
                using (Session<JObject, JObject> sessionJsonImpl =
                    new SessionJsonImpl<JObject>(envelopeEncryptionJsonImpl))
                {
                    Asherah.AppEncryption.Util.Json testJson = new Asherah.AppEncryption.Util.Json();
                    testJson.Put("Test", testData);

                    string persistenceKey = sessionJsonImpl.Store(testJson.ToJObject(), dataPersistence);

                    Option<JObject> testJson2 = sessionJsonImpl.Load(persistenceKey, dataPersistence);
                    Assert.True(testJson2.IsSome);
                    string resultData = ((JObject)testJson2)["Test"].ToObject<string>();

                    Assert.Equal(testData, resultData);
                }
            }
        }
    }
}
