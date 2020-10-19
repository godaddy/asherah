using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.OpenSSL
{
    [Collection("Logger Fixture collection")]
    public class OpenSSLCryptProtectMemoryTests : IDisposable
    {
        private readonly OpenSSL11ProtectedMemoryAllocatorLP64 linuxOpenSSL11ProtectedMemoryAllocatorLP64;
        private readonly SystemInterface systemInterface;
        private readonly IConfiguration configuration;

        public OpenSSLCryptProtectMemoryTests()
        {
            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
#if DEBUG
                {"openSSLPath", @"C:\Program Files\OpenSSL"},
#endif
            }).Build();
            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
                linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);
            }
        }

        public void Dispose()
        {
            linuxOpenSSL11ProtectedMemoryAllocatorLP64?.Dispose();
        }

        [SkippableFact]
        private void TestProtectAfterDispose()
        {
            var cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, configuration);
            cryptProtectMemory.Dispose();
            Assert.Throws<LibcOperationFailedException>(() => cryptProtectMemory.CryptProtectMemory(IntPtr.Zero, 0));
        }

        [SkippableFact]
        private void TestUnprotectAfterDispose()
        {
            var cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, configuration);
            cryptProtectMemory.Dispose();
            Assert.Throws<LibcOperationFailedException>(() => cryptProtectMemory.CryptUnprotectMemory(IntPtr.Zero, 0));
        }
    }
}
