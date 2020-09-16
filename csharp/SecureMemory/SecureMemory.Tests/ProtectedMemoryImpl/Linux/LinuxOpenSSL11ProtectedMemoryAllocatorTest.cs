using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class LinuxOpenSSL11ProtectedMemoryAllocatorTest
    {
        private LinuxOpenSSL11ProtectedMemoryAllocatorLP64 linuxOpenSSL11ProtectedMemoryAllocatorLP64;

        public LinuxOpenSSL11ProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64();
            }
        }

        [Fact]
        private void TestGetResourceCore()
        {
            if (linuxOpenSSL11ProtectedMemoryAllocatorLP64 == null)
            {
                return;
            }

            Assert.Equal(4, linuxOpenSSL11ProtectedMemoryAllocatorLP64.GetRlimitCoreResource());
        }

        [Fact]
        private void TestZeroMemory()
        {
            if (linuxOpenSSL11ProtectedMemoryAllocatorLP64 == null)
            {
                return;
            }

            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = linuxOpenSSL11ProtectedMemoryAllocatorLP64.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);

                linuxOpenSSL11ProtectedMemoryAllocatorLP64.ZeroMemory(pointer, length);
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(new byte[] { 0, 0, 0, 0 }, retValue);
            }
            finally
            {
                linuxOpenSSL11ProtectedMemoryAllocatorLP64.Free(pointer, length);
            }
        }
    }
}
