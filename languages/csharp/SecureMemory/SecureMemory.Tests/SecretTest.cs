using System;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class SecretTest
    {
        private readonly Mock<Secret> secretMock;

        public SecretTest()
        {
            secretMock = new Mock<Secret>();
        }

        [Fact]
        private void TestWithSecretBytesActionOfByte()
        {
            byte[] secretBytes = { 0, 1 };
            Action<byte[]> actionWithSecret = actualBytes =>
            {
                Assert.Equal(secretBytes, actualBytes);
            };

            secretMock.Setup(x => x.WithSecretBytes(It.IsAny<Action<byte[]>>())).CallBase();
            secretMock.Setup(x => x.WithSecretBytes(It.IsAny<Func<byte[], bool>>()))
                .Returns<Func<byte[], bool>>(action => action(secretBytes));
            secretMock.Object.WithSecretBytes(actionWithSecret);
        }

        [Fact]
        private void TestWithSecretUtf8CharsActionOfChar()
        {
            char[] secretChars = { (char)0, (char)1 };
            Action<char[]> actionWithSecret = actualChars =>
            {
                Assert.Equal(secretChars, actualChars);
            };

            secretMock.Setup(x => x.WithSecretUtf8Chars(It.IsAny<Action<char[]>>())).CallBase();
            secretMock.Setup(x => x.WithSecretUtf8Chars(It.IsAny<Func<char[], bool>>()))
                .Returns<Func<char[], bool>>(action => action(secretChars));
            secretMock.Object.WithSecretUtf8Chars(actionWithSecret);
        }
    }
}
