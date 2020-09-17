using System;
using System.Diagnostics;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemorySecretFactoryTest : IDisposable
    {
        private readonly ProtectedMemorySecretFactory protectedMemorySecretFactory;

        public ProtectedMemorySecretFactoryTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("ProtectedMemorySecretFactoryTest ctor");
            protectedMemorySecretFactory = new ProtectedMemorySecretFactory();
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemorySecretFactoryTest.Dispose");
            protectedMemorySecretFactory.Dispose();
        }

        // TODO Mocking static methods is not yet possible in Moq framework.
        // If it gets possible then we can add test these flows
        [Fact]
        private void TestProtectedMemorySecretFactoryWithMac()
        {
        }

        [Fact]
        private void TestProtectedMemorySecretFactoryWithLinux()
        {
        }

        [Fact]
        private void TestProtectedMemorySecretFactoryWithWindowsShouldFail()
        {
        }

        [Fact]
        private void TestCreateSecretByteArray()
        {
            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestCreateSecretByteArray");
            using Secret secret = protectedMemorySecretFactory.CreateSecret(new byte[] { 0, 1 });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestCreateSecretCharArray");
            using Secret secret = protectedMemorySecretFactory.CreateSecret(new[] { 'a', 'b' });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
        }
    }
}
