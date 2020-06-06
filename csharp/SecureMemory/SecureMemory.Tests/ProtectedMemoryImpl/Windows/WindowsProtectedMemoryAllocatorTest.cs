using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Windows
{
    [Collection("Logger Fixture collection")]
    public class WindowsProtectedMemoryAllocatorTest
    {
        private WindowsProtectedMemoryAllocatorLLP64 windowsProtectedMemoryAllocator;

        public WindowsProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                windowsProtectedMemoryAllocator = new WindowsProtectedMemoryAllocatorVirtualAlloc();
            }
        }

        [Fact]
        private void TestAllocSuccess()
        {
            if (windowsProtectedMemoryAllocator == null)
            {
                return;
            }

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

        [Fact]
        private void TestZeroMemory()
        {
            if (windowsProtectedMemoryAllocator == null)
            {
                return;
            }

            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = windowsProtectedMemoryAllocator.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);

                windowsProtectedMemoryAllocator.ZeroMemory(pointer, length);
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(new byte[] { 0, 0, 0, 0 }, retValue);
            }
            finally
            {
                windowsProtectedMemoryAllocator.Free(pointer, length);
            }
        }
    }
}
