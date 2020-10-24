using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Windows
{
    [Collection("Logger Fixture collection")]
    public class WindowsProtectedMemoryAllocatorTest : IDisposable
    {
        private readonly WindowsProtectedMemoryAllocatorLLP64 windowsProtectedMemoryAllocator;

        public WindowsProtectedMemoryAllocatorTest()
        {
            TraceListenerConfig.ConfigureTraceListener();

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "minimumWorkingSetSize", "33554430"},
                { "maximumWorkingSetSize", "67108860"},
            }).Build();

            var systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                windowsProtectedMemoryAllocator = new WindowsProtectedMemoryAllocatorLLP64(
                    configuration,
                    systemInterface,
                    new WindowsMemoryEncryption());
            }
        }

        public void Dispose()
        {
            windowsProtectedMemoryAllocator?.Dispose();
        }

        [SkippableFact]
        private void TestAllocSuccess()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            IntPtr pointer = windowsProtectedMemoryAllocator.Alloc(1);
            try
            {
                // just do some sanity checks
                Marshal.WriteByte(pointer, 0, 1);
                Assert.Equal(1, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                windowsProtectedMemoryAllocator.Free(pointer, 1);
            }
        }
    }
}
