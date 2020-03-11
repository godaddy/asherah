using System;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Envelope
{
    [Collection("Logger Fixture collection")]
    public class KeyMetaTest
    {
        private const string KeyId = "key1";
        private readonly DateTimeOffset created = DateTimeOffset.UtcNow.Truncate(TimeSpan.FromMinutes(1));

        private readonly KeyMeta keyMeta;

        public KeyMetaTest()
        {
            keyMeta = new KeyMeta(KeyId, created);
        }

        [Fact]
        private void TestConstructorRegular()
        {
            KeyMeta meta = new KeyMeta(KeyId, created);
            Assert.NotNull(meta);
            Assert.Equal(KeyId, meta.KeyId);
            Assert.Equal(created, meta.Created);
        }

        [Fact]
        private void TestConstructorJson()
        {
            Asherah.AppEncryption.Util.Json keyMetaJson = new Asherah.AppEncryption.Util.Json();
            keyMetaJson.Put("KeyId", KeyId);
            keyMetaJson.Put("Created", created);

            KeyMeta meta = new KeyMeta(keyMetaJson);
            Assert.NotNull(meta);
            Assert.Equal(KeyId, meta.KeyId);
            Assert.Equal(created, meta.Created);
        }

        [Fact]
        private void TestToJson()
        {
            JObject metaJson = keyMeta.ToJson();
            Assert.Equal(KeyId, metaJson.GetValue("KeyId").ToObject<string>());
            Assert.Equal(created.ToUnixTimeSeconds(), metaJson.GetValue("Created").ToObject<long>());
        }

        [Fact]
        private void TestHashCodeAndEqualsSymmetric()
        {
            KeyMeta otherKeyMeta = new KeyMeta(KeyId, created);
            Assert.True(keyMeta.GetHashCode() == otherKeyMeta.GetHashCode());
            Assert.NotSame(keyMeta, otherKeyMeta);
            Assert.True(keyMeta.Equals(otherKeyMeta) && otherKeyMeta.Equals(keyMeta));
        }

        [Fact]
        private void TestEqualsWithSameInstance()
        {
            // ReSharper disable once EqualExpressionComparison
            Assert.True(keyMeta.Equals(keyMeta));
        }

        [Fact]
        private void TestEqualsWithNull()
        {
            Assert.False(keyMeta.Equals(null));
        }

        [Fact]
        private void TestEqualsWithDifferentClass()
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            Assert.False(keyMeta.Equals("blah"));
        }

        [Fact]
        private void TestEqualsWithDifferentKeyId()
        {
            KeyMeta otherKeyMeta = new KeyMeta("some_other_keyid", created);
            Assert.False(keyMeta.Equals(otherKeyMeta));
            Assert.False(otherKeyMeta.Equals(keyMeta));
        }

        [Fact]
        private void TestEqualsWithDifferentCreated()
        {
            KeyMeta otherKeyMeta = new KeyMeta(KeyId, created.AddMinutes(3));
            Assert.False(keyMeta.Equals(otherKeyMeta));
            Assert.False(otherKeyMeta.Equals(keyMeta));
        }
    }
}
