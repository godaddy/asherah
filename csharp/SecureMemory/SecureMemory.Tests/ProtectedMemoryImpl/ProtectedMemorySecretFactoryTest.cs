using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;
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

        [SkippableFact]
        private void TestOpenSSLConfiguration()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"secureHeapEngine", "openssl11"}
            }).Build();

            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestOpenSSLConfiguration");
            using var _ = new ProtectedMemorySecretFactory(config);
        }

        [Fact]
        private void TestMmapConfiguration()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"secureHeapEngine", "mmap"}
            }).Build();

            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestMmapConfiguration");
            using var _ = new ProtectedMemorySecretFactory(config);
        }

        [Fact]
        private void TestInvalidConfiguration()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"secureHeapEngine", "magic-heap-engine2"}
            }).Build();

            Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestMmapConfiguration");
            Assert.Throws<PlatformNotSupportedException>(() =>
            {
                using var _ = new ProtectedMemorySecretFactory(config);
            });
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
        private void TestCreateSecretIntPtr()
        {
            var bytes = new byte[] {0, 1};
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestCreateSecretIntPtr");
                using (var factory = new ProtectedMemorySecretFactory(configuration))
                {
                    using Secret secret = factory.CreateSecret(handle.AddrOfPinnedObject(), (ulong)bytes.LongLength);
                    Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
                }
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        private void TestCreateSecretIntPtrZero()
        {
            var bytes = new byte[] { 0, 1 };
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var ptr = IntPtr.Zero;
            var len = 100;
            try
            {
                Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestCreateSecretByteArray");
                using (var factory = new ProtectedMemorySecretFactory(configuration))
                {
                    Assert.Throws<ArgumentNullException>(() =>
                    {
                        using Secret secret = factory.CreateSecret(ptr, (ulong)len);
                    });
                }
            }
            finally
            {
                handle.Free();
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

        [Fact]
        private void TestMultipleRefCount()
        {
            using var _ = new ProtectedMemorySecretFactory(configuration);
            using var __ = new ProtectedMemorySecretFactory(configuration);
        }

        [Fact]
        private void TestRequiredDisposeIntPtrSuccess()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"requireSecretDisposal", "true"}
            }).Build();

            var bytes = new byte[] {0, 1};
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Debug.WriteLine("ProtectedMemorySecretFactoryTest.TestRequiredDisposeIntPtrSuccess");
                using (var factory = new ProtectedMemorySecretFactory(config))
                {
                    using Secret secret = factory.CreateSecret(handle.AddrOfPinnedObject(), (ulong)bytes.LongLength);
                    Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
                }
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
