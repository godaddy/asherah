using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Libc
{
    /// <summary>
    /// These tests are a bit impure and expect the platform to be based on libc implementations
    /// Pure tests have been promoted to ProtectedMemoryAllocatorTest.cs
    /// </summary>
    [Collection("Logger Fixture collection")]
    public class LibcProtectedMemoryAllocatorTest : IDisposable
    {
        private readonly LibcLP64 libc;
        private readonly LibcProtectedMemoryAllocatorLP64 libcProtectedMemoryAllocator;
        private readonly Mock<MacOSProtectedMemoryAllocatorLP64> macOsProtectedMemoryAllocatorMock;
        private readonly Mock<LinuxProtectedMemoryAllocatorLP64> linuxProtectedMemoryAllocatorMock;

        public LibcProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);
            var systemInterface = SystemInterface.GetInstance();

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libc = new LinuxLibcLP64();
                libcProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64((LinuxLibcLP64)libc, systemInterface);
                linuxProtectedMemoryAllocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libc = new MacOSLibcLP64();
                libcProtectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64((MacOSLibcLP64)libc, systemInterface);
                macOsProtectedMemoryAllocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else
            {
                libc = null;
                libcProtectedMemoryAllocator = null;
                macOsProtectedMemoryAllocatorMock = null;
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.Dispose");
            libcProtectedMemoryAllocator?.Dispose();
        }

        [SkippableFact]
        private void TestDisableCoreDumpGlobally()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestDisableCoreDumpGlobally");

            // Mac allocator has global core dumps disabled on init
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.False(libcProtectedMemoryAllocator.AreCoreDumpsGloballyDisabled());
                libc.getrlimit(libcProtectedMemoryAllocator.GetRlimitCoreResource(), out var rlim);

                // Initial values here system dependent, assumes docker container spun up w/ unlimited
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_max);
                Assert.Equal(rlimit.UNLIMITED, rlim.rlim_cur);
            }

            libcProtectedMemoryAllocator.DisableCoreDumpGlobally();
            Assert.True(libcProtectedMemoryAllocator.AreCoreDumpsGloballyDisabled());
            rlimit zeroRlimit = rlimit.Zero();
            libc.getrlimit(libcProtectedMemoryAllocator.GetRlimitCoreResource(), out var newRlimit);
            Assert.Equal(zeroRlimit.rlim_cur, newRlimit.rlim_cur);
            Assert.Equal(zeroRlimit.rlim_max, newRlimit.rlim_max);
        }

        [SkippableFact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestAllocWithSetNoDumpErrorShouldFail");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    macOsProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    linuxProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
        }

        [SkippableFact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                Check.IntPtr(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [SkippableFact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            IntPtr fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcProtectedMemoryAllocator.Free(fakePtr, 0));
        }

        [SkippableFact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckZeroWithZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [SkippableFact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
