using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class SecureMemoryAllocatorTest : IDisposable
    {
        private static readonly IntPtr InvalidPointer = new(-1);

        private readonly ISecureMemoryAllocator secureMemoryAllocator;

        public SecureMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("SecureMemoryAllocatorTest ctor");
            secureMemoryAllocator = GetPlatformAllocator();
        }

        public void Dispose()
        {
            Debug.WriteLine("SecureMemoryAllocatorTest.Dispose");
            secureMemoryAllocator.Dispose();
        }

        internal static ISecureMemoryAllocator GetPlatformAllocator()
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
            var allocator1 = GetPlatformAllocator();
            var allocator2 = GetPlatformAllocator();
            Assert.NotNull(allocator1);
            Assert.NotNull(allocator2);
            allocator1.Dispose();
            allocator2.Dispose();
        }


        [Fact]
        private void TestAllocSuccess()
        {
            Debug.WriteLine("SecureMemoryAllocatorTest.TestAllocSuccess");
            var pointer = secureMemoryAllocator.Alloc(1);
            CheckIntPtr(pointer, "ISecureMemoryAllocator.Alloc");

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
    }
}
