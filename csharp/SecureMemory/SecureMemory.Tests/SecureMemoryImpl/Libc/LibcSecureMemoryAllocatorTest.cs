using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.Libc;
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
                libcSecureMemoryAllocator = new LinuxSecureMemoryAllocatorLP64();
                linuxSecureMemoryAllocatorMock = new Mock<LinuxSecureMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libcSecureMemoryAllocator = new MacOSSecureMemoryAllocatorLP64();
                macOsSecureMemoryAllocatorMock = new Mock<MacOSSecureMemoryAllocatorLP64>() { CallBase = true };
            }
            else
            {
                libcSecureMemoryAllocator = null;
                macOsSecureMemoryAllocatorMock = null;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Debug.WriteLine("LibcSecureMemoryAllocatorTest.Dispose");
            libcSecureMemoryAllocator?.Dispose();
        }

        [Fact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

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

        [Fact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            var pointer = libcSecureMemoryAllocator.Alloc(1);
            try
            {
                Check.ValidatePointer(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcSecureMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            var fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcSecureMemoryAllocator.Free(fakePtr, 0));
        }

        [Fact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.ValidatePointer(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.ValidatePointer(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckZeroWithZeroResult()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [Fact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [Fact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [Fact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Assert.SkipWhen(libcSecureMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcSecureMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
