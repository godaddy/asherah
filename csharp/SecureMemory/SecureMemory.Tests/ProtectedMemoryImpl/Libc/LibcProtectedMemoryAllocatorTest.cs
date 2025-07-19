using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.Libc;
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
        private readonly LibcProtectedMemoryAllocatorLP64 libcProtectedMemoryAllocator;
        private readonly Mock<MacOSProtectedMemoryAllocatorLP64> macOsProtectedMemoryAllocatorMock;
        private readonly Mock<LinuxProtectedMemoryAllocatorLP64> linuxProtectedMemoryAllocatorMock;

        public LibcProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest ctor");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libcProtectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
                linuxProtectedMemoryAllocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libcProtectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64();
                macOsProtectedMemoryAllocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
            }
            else
            {
                libcProtectedMemoryAllocator = null;
                macOsProtectedMemoryAllocatorMock = null;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.Dispose");
            libcProtectedMemoryAllocator?.Dispose();
        }


        [Fact]
        private void TestAllocWithResourceLimitZeroShouldFail()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var allocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(0);
                Assert.Throws<MemoryLimitException>(() =>
                {
                    allocatorMock.Object.Alloc(1);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var allocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(0);
                Assert.Throws<MemoryLimitException>(() =>
                {
                    allocatorMock.Object.Alloc(1);
                });
            }
        }

        [Fact]
        private void TestAllocWithResourceLimitMaxValueShouldSucceed()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var allocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(ulong.MaxValue);
                allocatorMock.Object.Alloc(1);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var allocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(ulong.MaxValue);
                allocatorMock.Object.Alloc(1);
            }
        }

        [Fact]
        private void TestAllocWithResourceLimitLargeValueShouldSucceed()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var allocatorMock = new Mock<MacOSProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(ulong.MaxValue - 1);
                allocatorMock.Object.Alloc(1);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var allocatorMock = new Mock<LinuxProtectedMemoryAllocatorLP64>() { CallBase = true };
                allocatorMock.Setup(x => x.GetMemlockResourceLimit()).Returns(ulong.MaxValue - 1);
                allocatorMock.Object.Alloc(1);
            }
        }

        [Fact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestAllocWithSetNoDumpErrorShouldFail");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOsProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                var exception = Assert.Throws<SecureMemoryAllocationFailedException>(() =>
                {
                    macOsProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
                Assert.IsType<LibcOperationFailedException>(exception.InnerException);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                linuxProtectedMemoryAllocatorMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                var exception = Assert.Throws<SecureMemoryAllocationFailedException>(() =>
                {
                    linuxProtectedMemoryAllocatorMock.Object.Alloc(1);
                });
                Assert.IsType<LibcOperationFailedException>(exception.InnerException);
            }
        }

        [Fact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            var pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                Check.ValidatePointer(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            var fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcProtectedMemoryAllocator.Free(fakePtr, 0));
        }

        [Fact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.ValidatePointer(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.ValidatePointer(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [Fact]
        private void TestCheckZeroWithZeroResult()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [Fact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [Fact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [Fact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Assert.SkipWhen(libcProtectedMemoryAllocator == null, "Test requires libc-based platform (Linux or macOS)");

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
