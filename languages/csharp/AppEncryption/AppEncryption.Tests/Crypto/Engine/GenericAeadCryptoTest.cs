using System;
using System.Security.Cryptography;
using System.Text;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Engine
{
    [Collection("Logger Fixture collection")]
    public abstract class GenericAeadCryptoTest
    {
        private readonly AeadEnvelopeCrypto crypto;

        private readonly RandomNumberGenerator random;

        protected GenericAeadCryptoTest()
        {
            crypto = GetCryptoInstance();
            random = RandomNumberGenerator.Create();
        }

        [Fact]
        public void GenerateKey()
        {
            CryptoKey key = crypto.GenerateKey();
            Assert.NotNull(key);

            // Ensure that the create date of the key is somewhat valid
            Assert.True(key.GetCreated() > DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.False(key.GetCreated() > DateTimeOffset.UtcNow.AddMinutes(1));

            Assert.False(key.WithKey(ByteArray.IsAllZeros));
        }

        [Theory]
        [InlineData("TestString")]
        [InlineData("ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ")]
        [InlineData(
            "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘")]
        public void RoundTripString(string testData)
        {
            CryptoKey key = crypto.GenerateKey();
            byte[] cipherText = crypto.Encrypt(Encoding.UTF8.GetBytes(testData), key);
            string plainText = Encoding.UTF8.GetString(crypto.Decrypt(cipherText, key));
            Assert.Equal(plainText, testData);
        }

        [Theory]
        [InlineData(8)]
        public void RoundTripRandom(int testSize)
        {
            byte[] testData = new byte[testSize];
            random.GetBytes(testData);
            CryptoKey key = crypto.GenerateKey();
            byte[] cipherText = crypto.Encrypt(testData, key);
            byte[] plainText = crypto.Decrypt(cipherText, key);
            Assert.Equal(plainText, testData);
        }

        [Fact]
        public void TestRoundTripStringWithWrongKeyDecryptShouldFail()
        {
            string testData = "blahblah";
            CryptoKey rightKey = crypto.GenerateKey();
            byte[] cipherText = crypto.Encrypt(Encoding.UTF8.GetBytes(testData), rightKey);
            CryptoKey wrongKey = crypto.GenerateKey();

            Assert.Throws<AppEncryptionException>(() => crypto.Decrypt(cipherText, wrongKey));
        }

        protected abstract AeadEnvelopeCrypto GetCryptoInstance();
    }
}
