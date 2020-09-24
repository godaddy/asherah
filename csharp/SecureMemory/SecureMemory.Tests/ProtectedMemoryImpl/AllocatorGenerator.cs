using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    public class AllocatorGenerator : IEnumerable<object[]>, IDisposable
    {
        private readonly List<object[]> allocators;

        public AllocatorGenerator()
        {
            allocators = new List<object[]>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                allocators.Add(new object[] { new MacOSProtectedMemoryAllocatorLP64() });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (LinuxOpenSSL11ProtectedMemoryAllocatorLP64.IsAvailable())
                {
                    allocators.Add(new object[] { new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(32000, 128) });
                }
                allocators.Add(new object[] { new LinuxProtectedMemoryAllocatorLP64() });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                allocators.Add(new object[] { new WindowsProtectedMemoryAllocatorVirtualAlloc() });
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
