using System;
using GoDaddy.Asherah.Crypto;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto
{
    [Collection("Logger Fixture collection")]
    public class NonceGeneratorTest
    {
        private const int BitsPerByte = 8;
        private const int NumBytes = 4;
        private readonly NonceGenerator generator = new NonceGenerator();

        [Fact]
        private void TestCreation()
        {
            NonceGenerator testGenerator = new NonceGenerator();
            Assert.NotNull(testGenerator);
        }

        [Fact]
        private void TestCreateNonceHappyPath()
        {
            const int numBits = BitsPerByte * NumBytes;
            byte[] nonce1 = generator.CreateNonce(numBits);
            byte[] nonce2 = generator.CreateNonce(numBits);
            Assert.Equal(NumBytes, nonce1.Length);
            Assert.Equal(NumBytes, nonce2.Length);
            Assert.NotEqual(nonce1, nonce2);
        }

        [Fact]
        private void TestInvalidArgument()
        {
            Assert.Throws<ArgumentException>(() => generator.CreateNonce(1));
        }
    }
}
