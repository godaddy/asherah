using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.Libc
{
    public class LibcMemoryAllocatorTest
    {
        private readonly LibcLP64 libc;
        private readonly LibcSecureMemoryAllocatorLP64 libcSecureMemoryAllocator;
        private readonly Mock<MacOSSecureMemoryAllocatorLP64> macOsSecureMemoryAllocatorMock;
        private readonly Mock<LinuxSecureMemoryAllocatorLP64> linuxSecureMemoryAllocatorMock;

        public LibcMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libc = new LinuxLibcLP64();
                libcSecureMemoryAllocator = new LinuxSecureMemoryAllocatorLP64((LinuxLibcLP64)libc);
                linuxSecureMemoryAllocatorMock = new Mock<LinuxSecureMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libc = new MacOSLibcLP64();
                libcSecureMemoryAllocator = new MacOSSecureMemoryAllocatorLP64((MacOSLibcLP64)libc);
                macOsSecureMemoryAllocatorMock = new Mock<MacOSSecureMemoryAllocatorLP64>() { CallBase = true };
            }
            else
            {
                libc = null;
                libcSecureMemoryAllocator = null;
                macOsSecureMemoryAllocatorMock = null;
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
                Assert.False(libcSecureMemoryAllocator.AreCoreDumpsGloballyDisabled());
                libc.getrlimit(libcSecureMemoryAllocator.GetRlimitCoreResource(), out var rlim);

                // Initial values here system dependent, assumes docker container spun up w/ unlimited
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_max);
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_cur);
            }

            libcSecureMemoryAllocator.DisableCoreDumpGlobally();
            Assert.True(libcSecureMemoryAllocator.AreCoreDumpsGloballyDisabled());
            rlimit zeroRlimit = rlimit.Zero();
            libc.getrlimit(libcSecureMemoryAllocator.GetRlimitCoreResource(), out var newRlimit);
            Assert.Equal(zeroRlimit.rlim_cur, newRlimit.rlim_cur);
            Assert.Equal(zeroRlimit.rlim_max, newRlimit.rlim_max);
        }
    }
}
