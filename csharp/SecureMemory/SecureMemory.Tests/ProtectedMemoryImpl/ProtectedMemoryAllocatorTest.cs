using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocatorTest : IDisposable
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly IProtectedMemoryAllocator protectedMemoryAllocator;

        public ProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                protectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                protectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                protectedMemoryAllocator = new WindowsProtectedMemoryAllocatorVirtualAlloc();
            }
            else
            {
                throw new NotSupportedException("Cannot determine platform for testing");
            }
        }

        public void Dispose()
        {
            protectedMemoryAllocator.Dispose();
        }

        private static void CheckIntPtr(IntPtr intPointer, string methodName)
        {
            if (intPointer == IntPtr.Zero || intPointer == InvalidPointer)
            {
                throw new LibcOperationFailedException(methodName, intPointer.ToInt64());
            }
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
