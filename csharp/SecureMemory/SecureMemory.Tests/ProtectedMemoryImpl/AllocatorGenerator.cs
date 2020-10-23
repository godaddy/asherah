using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    public class AllocatorGenerator : IEnumerable<object[]>, IDisposable
    {
        private readonly List<object[]> allocators;
        private readonly OpenSSLCryptProtectMemory cryptProtectMemory;

        public AllocatorGenerator()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
                {"openSSLPath", @"C:\Program Files\OpenSSL\bin"},
            }).Build();

            var systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            allocators = new List<object[]>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                allocators.Add(new object[] { new LibcProtectedMemoryAllocatorLP64(systemInterface) });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OpenSSL11ProtectedMemoryAllocatorLP64 openSSLAllocator;
                try
                {
                    var openSSLCryptoLibc = new OpenSSLCryptoLibc(configuration);

                    cryptProtectMemory = new OpenSSLCryptProtectMemory(
                        "aes-256-gcm",
                        systemInterface,
                        openSSLCryptoLibc
                    );

                    openSSLAllocator = new OpenSSL11ProtectedMemoryAllocatorLP64(
                        configuration,
                        systemInterface,
                        cryptProtectMemory,
                        openSSLCryptoLibc
                        );
                }
                catch (PlatformNotSupportedException)
                {
                    openSSLAllocator = null;
                }

                if (openSSLAllocator != null)
                {
                    allocators.Add(new object[] { openSSLAllocator });
                }
                allocators.Add(new object[] { new LibcProtectedMemoryAllocatorLP64(systemInterface) });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                allocators.Add(new object[] { new WindowsProtectedMemoryAllocatorLLP64(
                    configuration,
                    systemInterface,
                    new WindowsMemoryEncryption()
                    ) });
            }
        }

        public void Dispose()
        {
            foreach (var objArray in allocators)
            {
                ((IDisposable)objArray[0]).Dispose();
            }

            cryptProtectMemory?.Dispose();
        }

        public IEnumerator<object[]> GetEnumerator() => allocators.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
