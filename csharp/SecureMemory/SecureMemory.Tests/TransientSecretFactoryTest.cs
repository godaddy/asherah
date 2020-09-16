using System;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class TransientSecretFactoryTest : IDisposable
    {
        private readonly TransientSecretFactory transientSecretFactory;

        public TransientSecretFactoryTest()
        {
            Console.WriteLine("\nTransientSecretFactoryTest: New TransientSecretFactory");
            transientSecretFactory = new TransientSecretFactory();
        }

        public void Dispose()
        {
            transientSecretFactory.Dispose();
            Console.WriteLine("TransientSecretFactoryTest: Dispose TransientSecretFactory\n");
        }

        [Fact]
        private void TestCreateSecretByteArray()
        {
            Console.WriteLine("\nTestCreateSecretByteArray: Start");
            using Secret secret = transientSecretFactory.CreateSecret(new byte[] { 0, 1 });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
            Console.WriteLine("TestCreateSecretByteArray: Finish\n");
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Console.WriteLine("\nTestCreateSecretCharArray: Start");
            using Secret secret = transientSecretFactory.CreateSecret(new[] { 'a', 'b' });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
            Console.WriteLine("TestCreateSecretCharArray: Finish\n");
        }
    }
}
