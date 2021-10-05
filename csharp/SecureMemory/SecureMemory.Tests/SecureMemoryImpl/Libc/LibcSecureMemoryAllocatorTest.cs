using System;
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

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl.Libc
{
    /// <summary>
    /// These tests are a bit impure and expect the platform to be based on libc implementations
    /// Pure tests have been promoted to SecureMemoryAllocatorTest.cs
    /// </summary>
    [Collection("Logger Fixture collection")]
    public class LibcSecureMemoryAllocatorTest : IDisposable
    {
        private readonly LibcLP64 libc;
        private readonly LibcSecureMemoryAllocatorLP64 libcSecureMemoryAllocator;
        private readonly Mock<MacOSSecureMemoryAllocatorLP64> macOsSecureMemoryAllocatorMock;
        private readonly Mock<LinuxSecureMemoryAllocatorLP64> linuxSecureMemoryAllocatorMock;

        public LibcSecureMemoryAllocatorTest()
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

        public void Dispose()
        {
            Debug.WriteLine("LibcSecureMemoryAllocatorTest.Dispose");
            libcSecureMemoryAllocator?.Dispose();
        }

        [SkippableFact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestAllocWithSetNoDumpErrorShouldFail");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsSecureMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    macOsSecureMemoryAllocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxSecureMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                Assert.Throws<LibcOperationFailedException>(() =>
                {
                    linuxSecureMemoryAllocatorMock.Object.Alloc(1);
                });
            }
        }

        [SkippableFact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            IntPtr pointer = libcSecureMemoryAllocator.Alloc(1);
            try
            {
                Check.IntPtr(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcSecureMemoryAllocator.Free(pointer, 1);
            }
        }

        [SkippableFact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            IntPtr fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcSecureMemoryAllocator.Free(fakePtr, 0));
        }

        [SkippableFact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckZeroWithZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [SkippableFact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Skip.If(libc == null);

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
