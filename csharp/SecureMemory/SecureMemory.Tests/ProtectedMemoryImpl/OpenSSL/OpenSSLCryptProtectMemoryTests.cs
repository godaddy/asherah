using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.OpenSSL
{
    [Collection("Logger Fixture collection")]
    public class OpenSSLCryptProtectMemoryTests : IDisposable
    {
        private readonly OpenSSL11ProtectedMemoryAllocatorLP64 openSSL11ProtectedMemoryAllocatorLP64;
        private readonly SystemInterface systemInterface;
        private readonly IConfiguration configuration;
        private readonly IOpenSSLCrypto crypto;
        private OpenSSLCryptProtectMemory openSSLCryptProtectMemory;

        public OpenSSLCryptProtectMemoryTests()
        {
            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
#if DEBUG
                {"openSSLPath", @"C:\Program Files\OpenSSL\bin"},
#endif
            }).Build();
            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    crypto = new OpenSSLCryptoWindows(configuration);
                }
                else
                {
                    crypto = new OpenSSLCryptoLibc(configuration);
                    Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
                }

                openSSLCryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, crypto);

                openSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                    configuration,
                    systemInterface,
                    new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, crypto),
                    crypto);
            }
            catch (OpenSSLSecureHeapUnavailableException)
            {
                crypto = null;
                openSSLCryptProtectMemory = null;
                openSSL11ProtectedMemoryAllocatorLP64 = null;
            }
        }

        public void Dispose()
        {
            openSSL11ProtectedMemoryAllocatorLP64?.Dispose();
        }

        [SkippableFact]
        private void TestProtectAfterDispose()
        {
            if (crypto == null || openSSL11ProtectedMemoryAllocatorLP64 == null)
            {
                Skip.If(true, "No OpenSSL available for test");
                return;
            }

            openSSLCryptProtectMemory.Dispose();
            Assert.Throws<LibcOperationFailedException>(() => openSSLCryptProtectMemory.ProcessDecryptMemory(IntPtr.Zero, 0));
        }

        [SkippableFact]
        private void TestUnprotectAfterDispose()
        {
            if (crypto == null || openSSL11ProtectedMemoryAllocatorLP64 == null)
            {
                Skip.If(true, "No OpenSSL available for test");
                return;
            }


            openSSLCryptProtectMemory.Dispose();
            Assert.Throws<LibcOperationFailedException>(() => openSSLCryptProtectMemory.ProcessDecryptMemory(IntPtr.Zero, 0));
        }
    }
}
