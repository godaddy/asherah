using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    public class AllocatorGenerator : IEnumerable<object[]>, IDisposable
    {
        private readonly List<object[]> allocators;

        public AllocatorGenerator()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
            }).Build();

            var systemInterface = SystemInterface.GetInstance();

            allocators = new List<object[]>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                allocators.Add(new object[] { new LibcProtectedMemoryAllocatorLP64(systemInterface) });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                LinuxOpenSSL11ProtectedMemoryAllocatorLP64 openSSLAllocator;
                try
                {
                    openSSLAllocator = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);
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
                allocators.Add(new object[] { new WindowsProtectedMemoryAllocatorLLP64(configuration, systemInterface) });
            }
        }

        public void Dispose()
        {
            foreach (var objArray in allocators)
            {
                ((IDisposable)objArray[0]).Dispose();
            }
        }

        public IEnumerator<object[]> GetEnumerator() => allocators.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
