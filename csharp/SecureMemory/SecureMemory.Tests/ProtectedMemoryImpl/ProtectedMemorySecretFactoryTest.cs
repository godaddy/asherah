using System;
using System.Collections.Generic;
using System.Diagnostics;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemorySecretFactoryTest
    {
        private readonly IConfiguration configuration;

        public ProtectedMemorySecretFactoryTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configDictionary = new Dictionary<string, string>();
            configDictionary["debugSecrets"] = "true";

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDictionary)
                .Build();
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
            using (var factory = new ProtectedMemorySecretFactory(configuration))
            {
                using Secret secret = factory.CreateSecret(new byte[] { 0, 1 });
                Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
            }
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestCreateSecretCharArray");
            using (var factory = new ProtectedMemorySecretFactory(configuration))
            {
                using Secret secret = factory.CreateSecret(new[] { 'a', 'b' });
                Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
            }
        }

        [Fact]
        private void TestDoubleDispose()
        {
            var factory = new ProtectedMemorySecretFactory(configuration);
            factory.Dispose();
            Assert.Throws<Exception>(() => {
                factory.Dispose();
             });
        }
    }
}
