using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocatorTest : IDisposable
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly ISecureMemoryAllocator protectedMemoryAllocator;
        private IConfiguration configuration;

        public ProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
            }).Build();

            Debug.WriteLine("ProtectedMemoryAllocatorTest ctor");
            protectedMemoryAllocator = GetPlatformAllocator(configuration);
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.Dispose");
            protectedMemoryAllocator.Dispose();
        }

        internal static ISecureMemoryAllocator GetPlatformAllocator(IConfiguration configuration)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacOSProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration);
            }
            else
            {
                throw new NotSupportedException("Cannot determine platform for testing");
            }
        }

        private static void CheckIntPtr(IntPtr intPointer, string methodName)
        {
            if (intPointer == IntPtr.Zero || intPointer == InvalidPointer)
            {
                throw new LibcOperationFailedException(methodName, intPointer.ToInt64());
            }
        }

        [Fact]
        private void TestTwoAllocatorInstances()
        {
            var allocator1 = GetPlatformAllocator(configuration);
            var allocator2 = GetPlatformAllocator(configuration);
            Assert.NotNull(allocator1);
            Assert.NotNull(allocator2);
            allocator1.Dispose();
            allocator2.Dispose();
        }

        [Fact]
        private void TestAllocSuccess()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.TestAllocSuccess");
            var pointer = protectedMemoryAllocator.Alloc(1);
            CheckIntPtr(pointer, "ISecureMemoryAllocator.Alloc");

            try
            {
                // just do some sanity checks
                Marshal.WriteByte(pointer, 0, 1);
                Assert.Equal(1, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                protectedMemoryAllocator.Free(pointer, 1);
            }
        }
    }
}
