using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class LinuxOpenSSL11ProtectedMemoryAllocatorTest : IDisposable
    {
        private LinuxOpenSSL11ProtectedMemoryAllocatorLP64 linuxOpenSSL11ProtectedMemoryAllocatorLP64;

        public LinuxOpenSSL11ProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
                linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(32000, 128);
            }
        }

        public void Dispose()
        {
            linuxOpenSSL11ProtectedMemoryAllocatorLP64?.Dispose();
            Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorTest Dispose\n");
        }

        [Fact]
        private void TestGetResourceCore()
        {
            Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest TestGetResourceCore");
            if (linuxOpenSSL11ProtectedMemoryAllocatorLP64 == null)
            {
                return;
            }

            Assert.Equal(4, linuxOpenSSL11ProtectedMemoryAllocatorLP64.GetRlimitCoreResource());
            Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest TestGetResourceCore End");
        }

        [Fact]
        private void TestAllocFree()
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
            }
            finally
            {
                linuxOpenSSL11ProtectedMemoryAllocatorLP64.Free(pointer, length);
            }
        }
    }
}
