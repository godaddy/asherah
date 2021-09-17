using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.Libc
{
    public class LibcMemoryAllocatorTest: IDisposable
    {
        private readonly LibcLP64 libc;
        private readonly LibcMemoryAllocatorLP64 libcMemoryAllocator;

        public LibcMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libc = new LinuxLibcLP64();
                libcMemoryAllocator = new LinuxSecureMemoryAllocatorLP64((LinuxLibcLP64)libc);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libc = new MacOSLibcLP64();
                libcMemoryAllocator = new MacOSSecureMemoryAllocatorLP64((MacOSLibcLP64)libc);
            }
            else
            {
                libc = null;
                libcMemoryAllocator = null;
            }
        }
        [SkippableFact]
        private void TestDisableCoreDumpGlobally()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestDisableCoreDumpGlobally");

            // Mac allocator has global core dumps disabled on init
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.False(libcMemoryAllocator.AreCoreDumpsGloballyDisabled());
                libc.getrlimit(libcMemoryAllocator.GetRlimitCoreResource(), out var rlim);

                // Initial values here system dependent, assumes docker container spun up w/ unlimited
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_max);
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_cur);
            }

            libcMemoryAllocator.DisableCoreDumpGlobally();
            Assert.True(libcMemoryAllocator.AreCoreDumpsGloballyDisabled());
            rlimit zeroRlimit = rlimit.Zero();
            libc.getrlimit(libcMemoryAllocator.GetRlimitCoreResource(), out var newRlimit);
            Assert.Equal(zeroRlimit.rlim_cur, newRlimit.rlim_cur);
            Assert.Equal(zeroRlimit.rlim_max, newRlimit.rlim_max);
        }

        public void Dispose()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.Dispose");
            libcMemoryAllocator?.Dispose();
        }
    }
}
