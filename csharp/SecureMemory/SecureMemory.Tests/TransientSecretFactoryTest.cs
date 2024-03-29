using System;
using System.Collections.Generic;
using System.Diagnostics;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class TransientSecretFactoryTest : IDisposable
    {
        private readonly TransientSecretFactory transientSecretFactory;

        public TransientSecretFactoryTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            Debug.WriteLine("\nTransientSecretFactoryTest: New TransientSecretFactory");
            transientSecretFactory = new TransientSecretFactory(configuration);
        }

        public void Dispose()
        {
            transientSecretFactory.Dispose();
            Debug.WriteLine("TransientSecretFactoryTest: Dispose TransientSecretFactory\n");
        }

        [Fact]
        private void TestCreateSecretByteArray()
        {
            Debug.WriteLine("\nTestCreateSecretByteArray: Start");
            using Secret secret = transientSecretFactory.CreateSecret(new byte[] { 0, 1 });
            Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
            Debug.WriteLine("TestCreateSecretByteArray: Finish\n");
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Debug.WriteLine("\nTestCreateSecretCharArray: Start");
            using Secret secret = transientSecretFactory.CreateSecret(new[] { 'a', 'b' });
            Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
            Debug.WriteLine("TestCreateSecretCharArray: Finish\n");
        }
    }
}
