using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libc = new LinuxLibcLP64();
                libcProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64((LinuxLibcLP64)libc);
                linuxProtectedMemoryAllocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libc = new MacOSLibcLP64();
                libcProtectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64((MacOSLibcLP64)libc);
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

        [Fact]
        private void TestDisableCoreDumpGlobally()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestDisableCoreDumpGlobally");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

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

        [Fact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestAllocWithSetNoDumpErrorShouldFail");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

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

        [Fact]
        private void TestAllocWithCheckZeroErrorShouldFail()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestAllocWithCheckZeroErrorShouldFail");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsProtectedMemoryAllocatorMock.Setup(x => Check.Zero(It.IsAny<int>(), It.IsAny<string>()))
                    .Throws(new TargetInvocationException(new Exception()));
                Assert.Throws<TargetInvocationException>(() =>
                {
                    macOsProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocatorMock.Setup(x => Check.Zero(It.IsAny<int>(), It.IsAny<string>()))
                    .Throws(new TargetInvocationException(new Exception()));
                Assert.Throws<TargetInvocationException>(() =>
                {
                    linuxProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
        }

        [Fact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

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

        [Fact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckZeroWithZeroResult()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithZeroResult");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [Fact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [Fact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [Fact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");
            // Don't run libc tests on platforms that don't match libc/posix behaviors
            if (libc == null)
            {
                return;
            }

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
