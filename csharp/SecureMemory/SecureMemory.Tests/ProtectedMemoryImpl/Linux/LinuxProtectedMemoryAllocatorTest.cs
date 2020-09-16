using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class LinuxProtectedMemoryAllocatorTest : IDisposable
    {
        private LinuxProtectedMemoryAllocatorLP64 linuxProtectedMemoryAllocator;

        public LinuxProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
            }
        }

        public void Dispose()
        {
            linuxProtectedMemoryAllocator?.Dispose();
        }

        [Fact]
        private void TestGetResourceCore()
        {
            if (linuxProtectedMemoryAllocator == null)
            {
                return;
            }

            Assert.Equal(4, linuxProtectedMemoryAllocator.GetRlimitCoreResource());
        }

        [Fact]
        private void TestZeroMemory()
        {
            if (linuxProtectedMemoryAllocator == null)
            {
                return;
            }

            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = linuxProtectedMemoryAllocator.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);

                linuxProtectedMemoryAllocator.ZeroMemory(pointer, length);
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(new byte[] { 0, 0, 0, 0 }, retValue);
            }
            finally
            {
                linuxProtectedMemoryAllocator.Free(pointer, length);
            }
        }
    }
}
