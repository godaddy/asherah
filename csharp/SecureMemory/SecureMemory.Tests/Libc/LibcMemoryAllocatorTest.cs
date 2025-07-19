using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.SecureMemory.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.Libc
{
    public class LibcMemoryAllocatorTest : IDisposable
    {
        private readonly LibcMemoryAllocatorLP64 libcMemoryAllocator;

        public LibcMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LibcMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libcMemoryAllocator = new LinuxSecureMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libcMemoryAllocator = new MacOSSecureMemoryAllocatorLP64();
            }
            else
            {
                libcMemoryAllocator = null;
            }
        }

        [Fact]
        private void TestDisableCoreDumpGlobally()
        {
            Assert.SkipWhen(libcMemoryAllocator == null, "LibcMemoryAllocator is not supported on this platform. Skipping test.");

            Debug.WriteLine("LibcMemoryAllocatorTest.TestDisableCoreDumpGlobally");

            // Mac allocator has global core dumps disabled on init
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.False(libcMemoryAllocator.AreCoreDumpsGloballyDisabled());
                LibcLP64.getrlimit(libcMemoryAllocator.GetRlimitCoreResource(), out var rlim);

                // Initial values here system dependent, assumes docker container spun up w/ unlimited
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_max);
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_cur);
            }

            libcMemoryAllocator.DisableCoreDumpGlobally();
            Assert.True(libcMemoryAllocator.AreCoreDumpsGloballyDisabled());
            var zeroRlimit = rlimit.Zero();
            LibcLP64.getrlimit(libcMemoryAllocator.GetRlimitCoreResource(), out var newRlimit);
            Assert.Equal(zeroRlimit.rlim_cur, newRlimit.rlim_cur);
            Assert.Equal(zeroRlimit.rlim_max, newRlimit.rlim_max);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Debug.WriteLine("LibcMemoryAllocatorTest.Dispose");
            libcMemoryAllocator?.Dispose();
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
            Debug.WriteLine("SecureMemoryAllocatorTest.TestSetReadWriteAccess");
            var pointer = libcMemoryAllocator.Alloc(1);

            try
            {
                libcMemoryAllocator.SetReadWriteAccess(pointer, 1);

                // Verifies we can write and read back
                Marshal.WriteByte(pointer, 0, 42);
                Assert.Equal(42, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                libcMemoryAllocator.Free(pointer, 1);
            }
        }
    }
}
