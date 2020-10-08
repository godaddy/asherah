using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class OpenSSLCryptProtectMemoryTests : IDisposable
    {
        private readonly LinuxOpenSSL11ProtectedMemoryAllocatorLP64 linuxOpenSSL11ProtectedMemoryAllocatorLP64;

        public OpenSSLCryptProtectMemoryTests()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
            }).Build();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
                linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);
            }
        }

        public void Dispose()
        {
            linuxOpenSSL11ProtectedMemoryAllocatorLP64?.Dispose();
        }

        [SkippableFact]
        private void TestProtectAfterDispose()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            var cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", linuxOpenSSL11ProtectedMemoryAllocatorLP64);
            cryptProtectMemory.Dispose();
            Assert.Throws<Exception>(() => cryptProtectMemory.CryptProtectMemory(IntPtr.Zero, 0));
        }

        [SkippableFact]
        private void TestUnprotectAfterDispose()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            var cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", linuxOpenSSL11ProtectedMemoryAllocatorLP64);
            cryptProtectMemory.Dispose();
            Assert.Throws<Exception>(() => cryptProtectMemory.CryptUnprotectMemory(IntPtr.Zero, 0));
        }
    }
}
