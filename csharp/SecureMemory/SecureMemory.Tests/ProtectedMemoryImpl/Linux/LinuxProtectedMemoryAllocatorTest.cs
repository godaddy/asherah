using System;
using System.Diagnostics;
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
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LinuxProtectedMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("LinuxProtectedMemoryAllocatorTest.Dispose");
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
        private void TestAllocFree()
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
            }
            finally
            {
                linuxProtectedMemoryAllocator.Free(pointer, length);
            }
        }
    }
}
