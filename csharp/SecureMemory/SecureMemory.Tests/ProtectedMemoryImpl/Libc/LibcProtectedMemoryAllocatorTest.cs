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
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.Dispose");
            libcProtectedMemoryAllocator?.Dispose();
        }


        [SkippableFact]
        private void TestAllocWithResourceLimitZeroShouldFail()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

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

        [SkippableFact]
        private void TestAllocWithResourceLimitMaxValueShouldSucceed()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

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

        [SkippableFact]
        private void TestAllocWithResourceLimitLargeValueShouldSucceed()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

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

        [SkippableFact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

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

        [SkippableFact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            var pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                Check.IntPointer(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [SkippableFact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            var fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcProtectedMemoryAllocator.Free(fakePtr, 0));
        }

        [SkippableFact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPointer(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPointer(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckZeroWithZeroResult()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [SkippableFact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Skip.If(libcProtectedMemoryAllocator == null);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }
    }
}
