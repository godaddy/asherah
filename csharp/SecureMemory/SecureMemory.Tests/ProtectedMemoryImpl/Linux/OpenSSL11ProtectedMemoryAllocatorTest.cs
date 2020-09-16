using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class OpenSSL11LinuxProtectedMemoryAllocatorTest
    {
        private OpenSSL11LinuxProtectedMemoryAllocatorLP64 openSSL11linuxProtectedMemoryAllocator;

        public OpenSSL11LinuxProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                openSSL11linuxProtectedMemoryAllocator = new OpenSSL11LinuxProtectedMemoryAllocatorLP64();
            }
        }

        [Fact]
        private void TestGetResourceCore()
        {
            if (linuxProtectedMemoryAllocator == null)
            {
                return;
            }

            Assert.Equal(4, openSSL11linuxProtectedMemoryAllocator.GetRlimitCoreResource());
        }

        [Fact]
        private void TestZeroMemory()
        {
            if (openSSL11linuxProtectedMemoryAllocator == null)
            {
                return;
            }

            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = openSSL11linuxProtectedMemoryAllocator.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);

                openSSL11linuxProtectedMemoryAllocator.ZeroMemory(pointer, length);
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(new byte[] { 0, 0, 0, 0 }, retValue);
            }
            finally
            {
                openSSL11linuxProtectedMemoryAllocator.Free(pointer, length);
            }
        }
    }
}
