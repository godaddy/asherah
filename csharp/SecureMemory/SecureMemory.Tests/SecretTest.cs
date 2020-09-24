using System;
using System.Diagnostics;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class SecretTest : IDisposable
    {
        private readonly Mock<Secret> secretMock;

        public SecretTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("\nSecretTest ctor");
            secretMock = new Mock<Secret>();
        }

        public void Dispose()
        {
            Debug.WriteLine("SecretTest Dispose\n");
        }

        [Fact]
        private void TestWithSecretBytesActionOfByte()
        {
            Debug.WriteLine("\nTestWithSecretBytesActionOfByte: Start");
            byte[] secretBytes = { 0, 1 };
            Action<byte[]> actionWithSecret = actualBytes =>
            {
                Assert.Equal(secretBytes, actualBytes);
            };

            secretMock.Setup(x => x.WithSecretBytes(It.IsAny<Action<byte[]>>())).CallBase();
            secretMock.Setup(x => x.WithSecretBytes(It.IsAny<Func<byte[], bool>>()))
                .Returns<Func<byte[], bool>>(action => action(secretBytes));
            secretMock.Object.WithSecretBytes(actionWithSecret);
            Debug.WriteLine("TestWithSecretBytesActionOfByte: Finish\n");
        }

        [Fact]
        private void TestWithSecretUtf8CharsActionOfChar()
        {
            Debug.WriteLine("\nTestWithSecretUtf8CharsActionOfChar: Start");
            char[] secretChars = { (char)0, (char)1 };
            Action<char[]> actionWithSecret = actualChars =>
            {
                Assert.Equal(secretChars, actualChars);
            };

            secretMock.Setup(x => x.WithSecretUtf8Chars(It.IsAny<Action<char[]>>())).CallBase();
            secretMock.Setup(x => x.WithSecretUtf8Chars(It.IsAny<Func<char[], bool>>()))
                .Returns<Func<char[], bool>>(action => action(secretChars));
            secretMock.Object.WithSecretUtf8Chars(actionWithSecret);
            Debug.WriteLine("TestWithSecretUtf8CharsActionOfChar: Finish\n");
        }
    }
}
