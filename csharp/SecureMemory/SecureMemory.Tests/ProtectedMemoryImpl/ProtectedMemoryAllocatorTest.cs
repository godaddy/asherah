using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocatorTest : IDisposable
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly IProtectedMemoryAllocator protectedMemoryAllocator;
        private readonly IConfiguration configuration;
        private readonly SystemInterface systemInterface;

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

            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            Debug.WriteLine("ProtectedMemoryAllocatorTest ctor");
            protectedMemoryAllocator = GetPlatformAllocator();
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.Dispose");
            protectedMemoryAllocator.Dispose();
        }

        internal IProtectedMemoryAllocator GetPlatformAllocator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LibcProtectedMemoryAllocatorLP64(systemInterface);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new LibcProtectedMemoryAllocatorLP64(systemInterface);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsProtectedMemoryAllocatorLLP64(
                    configuration,
                    systemInterface,
                    new WindowsMemoryEncryption());
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
            var allocator1 = GetPlatformAllocator();
            var allocator2 = GetPlatformAllocator();
            Assert.NotNull(allocator1);
            Assert.NotNull(allocator2);
            allocator1.Dispose();
            allocator2.Dispose();
        }

        [Fact]
        private void TestSetNoAccess()
        {
        }

        [Fact]
        private void TestSetReadAccess()
        {
        }

        [Fact]
        private void TestSetReadWriteAccess()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.TestSetReadWriteAccess");
            IntPtr pointer = protectedMemoryAllocator.Alloc(1);

            try
            {
                protectedMemoryAllocator.SetReadWriteAccess(pointer, 1);
                CheckIntPtr(pointer, "IProtectedMemoryAllocator.Alloc");

                // Verifies we can write and read back
                Marshal.WriteByte(pointer, 0, 42);
                Assert.Equal(42, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                protectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestAllocSuccess()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.TestAllocSuccess");
            IntPtr pointer = protectedMemoryAllocator.Alloc(1);
            CheckIntPtr(pointer, "IProtectedMemoryAllocator.Alloc");

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

        [Fact]
        private void TestFree()
        {
        }
    }
}
