using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class SecureMemoryAllocatorTest : IDisposable
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly ISecureMemoryAllocator secureMemoryAllocator;
        private IConfiguration configuration;

        public SecureMemoryAllocatorTest()
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
            secureMemoryAllocator = GetPlatformAllocator(configuration);
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.Dispose");
            secureMemoryAllocator.Dispose();
        }

        internal ISecureMemoryAllocator GetPlatformAllocator(IConfiguration configuration)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxSecureMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacOSSecureMemoryAllocatorLP64();
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
            IntPtr pointer = secureMemoryAllocator.Alloc(1);

            try
            {
                secureMemoryAllocator.SetReadWriteAccess(pointer, 1);
                CheckIntPtr(pointer, "IProtectedMemoryAllocator.Alloc");

                // Verifies we can write and read back
                Marshal.WriteByte(pointer, 0, 42);
                Assert.Equal(42, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                secureMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestAllocSuccess()
        {
            Debug.WriteLine("ProtectedMemoryAllocatorTest.TestAllocSuccess");
            IntPtr pointer = secureMemoryAllocator.Alloc(1);
            CheckIntPtr(pointer, "IProtectedMemoryAllocator.Alloc");

            try
            {
                // just do some sanity checks
                Marshal.WriteByte(pointer, 0, 1);
                Assert.Equal(1, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                secureMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestFree()
        {
        }
    }
}
