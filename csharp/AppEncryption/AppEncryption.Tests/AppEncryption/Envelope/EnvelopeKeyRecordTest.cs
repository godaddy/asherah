using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using LanguageExt;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Envelope
{
    [Collection("Logger Fixture collection")]
    public class EnvelopeKeyRecordTest
    {
        private const string ParentKey = "key1";
        private const bool Revoked = true;
        private readonly DateTimeOffset created = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));
        private readonly byte[] encryptedKey = { 0, 1, 2, 3, 4 };
        private readonly DateTimeOffset parentCreated;
        private readonly KeyMeta parentKeyMeta;

        public EnvelopeKeyRecordTest()
        {
            // Have to init these in constructor
            parentCreated = created.AddSeconds(-1);
            parentKeyMeta = new KeyMeta(ParentKey, parentCreated);
        }

        [Fact]
        private void TestConstructorRegularWithoutRevokedShouldBeOptionalEmpty()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey);
            Assert.NotNull(record);
            Assert.Equal(created, record.Created);
            Assert.Equal(Option<KeyMeta>.Some(parentKeyMeta), record.ParentKeyMeta);
            Assert.Equal(encryptedKey, record.EncryptedKey);
            Assert.Equal(Option<bool>.None, record.Revoked);
        }

        [Fact]
        private void TestConstructorRegularWithParentKeyMetaAndRevoked()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey, Revoked);
            Assert.NotNull(record);
            Assert.Equal(created, record.Created);
            Assert.Equal(Option<KeyMeta>.Some(parentKeyMeta), record.ParentKeyMeta);
            Assert.Equal(encryptedKey, record.EncryptedKey);
            Assert.Equal(Option<bool>.Some(Revoked), record.Revoked);
        }

        [Fact]
        private void TestConstructorRegularWithNullParentKeyMetaAndNullRevokedShouldBeOptionalEmpty()
        {
            EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, null, encryptedKey, null);
            Assert.NotNull(record);
            Assert.Equal(created, record.Created);
            Assert.Equal(Option<KeyMeta>.None, record.ParentKeyMeta);
            Assert.Equal(encryptedKey, record.EncryptedKey);
            Assert.Equal(Option<bool>.None, record.Revoked);
        }

        [Fact]
        private void TestConstructorJsonWithParentKeyMetaAndRevoked()
        {
            Asherah.AppEncryption.Util.Json parentKeyMetaJson = new Asherah.AppEncryption.Util.Json();
            parentKeyMetaJson.Put("KeyId", ParentKey);
            parentKeyMetaJson.Put("Created", parentCreated);

            Asherah.AppEncryption.Util.Json envelopeKeyRecordJson = new Asherah.AppEncryption.Util.Json();
            envelopeKeyRecordJson.Put("Created", created);
            envelopeKeyRecordJson.Put("ParentKeyMeta", parentKeyMetaJson);
            envelopeKeyRecordJson.Put("Key", encryptedKey);
            envelopeKeyRecordJson.Put("Revoked", Revoked);

            EnvelopeKeyRecord record = new EnvelopeKeyRecord(envelopeKeyRecordJson);
            Assert.NotNull(record);
            Assert.Equal(created, record.Created);
            Assert.Equal(Option<KeyMeta>.Some(parentKeyMeta), record.ParentKeyMeta);
            Assert.Equal(encryptedKey, record.EncryptedKey);
            Assert.Equal(Option<bool>.Some(Revoked), record.Revoked);
        }

        [Fact]
        private void TestConstructorJsonWithNullParentKeyMetaAndNullRevokedShouldBeOptionalEmptyAndFalse()
        {
            Asherah.AppEncryption.Util.Json envelopeKeyRecordJson = new Asherah.AppEncryption.Util.Json();
            envelopeKeyRecordJson.Put("Created", created);
            envelopeKeyRecordJson.Put("Key", encryptedKey);

            EnvelopeKeyRecord record = new EnvelopeKeyRecord(envelopeKeyRecordJson);
            Assert.NotNull(record);
            Assert.Equal(created, record.Created);
            Assert.Equal(Option<KeyMeta>.None, record.ParentKeyMeta);
            Assert.Equal(encryptedKey, record.EncryptedKey);
            Assert.Equal(Option<bool>.None, record.Revoked);
        }

        [Fact]
        private void TestToJsonWithParentKeyMetaAndRevoked()
        {
            EnvelopeKeyRecord envelopeKeyRecord = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey, Revoked);

            JObject recordJson = envelopeKeyRecord.ToJson();
            Assert.Equal(created.ToUnixTimeSeconds(), recordJson.GetValue("Created").ToObject<long>());
            Assert.Equal(parentCreated.ToUnixTimeSeconds(), recordJson["ParentKeyMeta"]["Created"].ToObject<long>());
            Assert.Equal(ParentKey, recordJson["ParentKeyMeta"]["KeyId"].ToObject<string>());
            Assert.Equal(encryptedKey, Convert.FromBase64String(recordJson["Key"].ToString()));
            Assert.Equal(Revoked, recordJson["Revoked"].ToObject<bool>());
        }

        [Fact]
        private void TestToJsonWithNullParentKeyMetaAndNullRevokedShouldBeNull()
        {
            EnvelopeKeyRecord envelopeKeyRecord = new EnvelopeKeyRecord(created, null, encryptedKey);

            JObject recordJson = envelopeKeyRecord.ToJson();
            Assert.Equal(created.ToUnixTimeSeconds(), recordJson.GetValue("Created").ToObject<long>());
            Assert.Null(recordJson["ParentKeyMeta"]);
            Assert.Equal(encryptedKey, Convert.FromBase64String(recordJson["Key"].ToString()));
            Assert.Null(recordJson["Revoked"]);
        }
    }
}
