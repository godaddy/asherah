using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
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

            allocators = new List<object[]>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                allocators.Add(new object[] { new MacOSSecureMemoryAllocatorLP64() });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                allocators.Add(new object[] { new LinuxSecureMemoryAllocatorLP64() });
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
