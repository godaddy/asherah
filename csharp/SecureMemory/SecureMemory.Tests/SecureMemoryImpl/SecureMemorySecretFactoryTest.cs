using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class SecureMemorySecretFactoryTest
    {
        private readonly IConfiguration configuration;

        public SecureMemorySecretFactoryTest()
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
        private void TestSecureMemorySecretFactoryWithMac()
        {
        }

        [Fact]
        private void TestSecureMemorySecretFactoryWithLinux()
        {
        }

        [Fact]
        private void TestSecureMemorySecretFactoryWithWindowsShouldFail()
        {
        }

        [Fact]
        private void TestMmapConfiguration()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"secureHeapEngine", "mmap"}
            }).Build();

            Debug.WriteLine("SecureMemorySecretFactoryTest.TestMmapConfiguration");
            using (var factory = new SecureMemorySecretFactory(configuration))
            {
            }
        }

        [Fact]
        private void TestInvalidConfiguration()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"secureHeapEngine", "magic-heap-engine2"}
            }).Build();

            Debug.WriteLine("SecureMemorySecretFactoryTest.TestMmapConfiguration");
            Assert.Throws<PlatformNotSupportedException>(() =>
            {
                using (var factory = new SecureMemorySecretFactory(configuration))
                {
                }
            });
        }

        [Fact]
        private void TestCreateSecretByteArray()
        {
            Debug.WriteLine("SecureMemorySecretFactoryTest.TestCreateSecretByteArray");
            using (var factory = new SecureMemorySecretFactory(configuration))
            {
                using Secret secret = factory.CreateSecret(new byte[] { 0, 1 });
                Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
            }
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Debug.WriteLine("SecureMemorySecretFactoryTest.TestCreateSecretCharArray");
            using (var factory = new SecureMemorySecretFactory(configuration))
            {
                using Secret secret = factory.CreateSecret(new[] { 'a', 'b' });
                Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
            }
        }

        [Fact]
        private void TestDoubleDispose()
        {
            var factory = new SecureMemorySecretFactory(configuration);
            factory.Dispose();
            Assert.Throws<Exception>(() => {
                factory.Dispose();
             });
        }
    }
}
