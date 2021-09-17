using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl.Linux
{
    [Collection("Logger Fixture collection")]
    public class LinuxProtectedMemoryAllocatorTest : IDisposable
    {
        private LinuxSecureMemoryAllocatorLP64 linuxProtectedMemoryAllocator;

        public LinuxProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LinuxProtectedMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocator = new LinuxSecureMemoryAllocatorLP64();
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("LinuxProtectedMemoryAllocatorTest.Dispose");
            linuxProtectedMemoryAllocator?.Dispose();
        }

        [SkippableFact]
        private void TestSetNoDumpInvalidLength()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            IntPtr fakeValidPointer = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<Exception>(() => linuxProtectedMemoryAllocator.SetNoDump(fakeValidPointer, 0));
        }

        [SkippableFact]
        private void TestGetResourceCore()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            Assert.Equal(4, linuxProtectedMemoryAllocator.GetRlimitCoreResource());
        }

        [SkippableFact]
        private void TestAllocFree()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

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
