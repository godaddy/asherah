using System;
using System.Reflection;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Libc
{
    [Collection("Logger Fixture collection")]
    public class LibcProtectedMemoryAllocatorTest
    {
        private readonly LibcLP64 libc;
        private readonly LibcProtectedMemoryAllocatorLP64 libcProtectedMemoryAllocator;
        private readonly Mock<MacOSProtectedMemoryAllocatorLP64> macOsProtectedMemoryAllocatorMock;
        private readonly Mock<LinuxProtectedMemoryAllocatorLP64> linuxProtectedMemoryAllocatorMock;

        public LibcProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libc = new LinuxLibcLP64();
                libcProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
                linuxProtectedMemoryAllocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libc = new MacOSLibcLP64();
                libcProtectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64();
                macOsProtectedMemoryAllocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
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
            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(1);

            try
            {
                libcProtectedMemoryAllocator.SetReadWriteAccess(pointer, 1);

                // Verifies we can write and read back
                Marshal.WriteByte(pointer, 0, 42);
                Assert.Equal(42, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestDisableCoreDumpGlobally()
        {
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
        private void TestAllocSuccess()
        {
            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                // just do some sanity checks
                Marshal.WriteByte(pointer, 0, 1);
                Assert.Equal(1, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("blah", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    macOsProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("blah", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    linuxProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
        }

        [Fact]
        private void TestAllocWithCheckZeroErrorShouldFail()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsProtectedMemoryAllocatorMock.Setup(x => x.CheckZero(It.IsAny<int>(), It.IsAny<string>()))
                    .Throws(new TargetInvocationException(new Exception()));
                Assert.Throws<TargetInvocationException>(() =>
                {
                    macOsProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocatorMock.Setup(x => x.CheckZero(It.IsAny<int>(), It.IsAny<string>()))
                    .Throws(new TargetInvocationException(new Exception()));
                Assert.Throws<TargetInvocationException>(() =>
                {
                    linuxProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
            }
        }

        [Fact]
        private void TestFree()
        {
        }

        [Fact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                libcProtectedMemoryAllocator.CheckIntPtr(pointer, "blah");
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                libcProtectedMemoryAllocator.CheckIntPtr(IntPtr.Zero, "blah");
            });
        }

        [Fact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                libcProtectedMemoryAllocator.CheckIntPtr(new IntPtr(-1), "blah");
            });
        }

        [Fact]
        private void TestCheckZeroWithZeroResult()
        {
            libcProtectedMemoryAllocator.CheckZero(0, "blah");
        }

        [Fact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Assert.Throws<LibcOperationFailedException>(() => { libcProtectedMemoryAllocator.CheckZero(1, "blah"); });
        }

        [Fact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            libcProtectedMemoryAllocator.CheckZero(0, "blah", new InvalidOperationException());
        }

        [Fact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                libcProtectedMemoryAllocator.CheckZero(1, "blah", new InvalidOperationException());
            });
        }
    }
}
