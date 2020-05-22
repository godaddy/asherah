using System;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocatorTest
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly IProtectedMemoryAllocator protectedMemoryAllocator;

        public ProtectedMemoryAllocatorTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                protectedMemoryAllocator = new LinuxProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                protectedMemoryAllocator = new MacOSProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                protectedMemoryAllocator = new WindowsProtectedMemoryAllocatorLLP64();
            }
            else
            {
                throw new NotSupportedException("Cannot determine platform for testing");
            }
        }

        private void CheckIntPtr(IntPtr intPointer, string methodName)
        {
            if (intPointer == IntPtr.Zero || intPointer == InvalidPointer)
            {
                throw new LibcOperationFailedException(methodName, intPointer.ToInt64());
            }
        }

        private void CheckZero(int result, string methodName)
        {
            if (result != 0)
            {
                // NOTE: Even though this references Win32 it actually returns
                // the last errno on non-Windows platforms.
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException(methodName, result, errno);
            }
        }

        private void CheckZero(int result, string methodName, Exception exceptionInProgress)
        {
            if (result != 0)
            {
                throw new LibcOperationFailedException(methodName, result, exceptionInProgress);
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
            IntPtr pointer = protectedMemoryAllocator.Alloc(1);

            try
            {
                protectedMemoryAllocator.SetReadWriteAccess(pointer, 1);

                // Verifies we can write and read back
                Marshal.WriteByte(pointer, 0, 42);
                Assert.Equal(42, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                protectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestAllocSuccess()
        {
            IntPtr pointer = protectedMemoryAllocator.Alloc(1);
            try
            {
                // just do some sanity checks
                Marshal.WriteByte(pointer, 0, 1);
                Assert.Equal(1, Marshal.ReadByte(pointer, 0));
            }
            finally
            {
                protectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestFree()
        {
        }

        [Fact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            IntPtr pointer = protectedMemoryAllocator.Alloc(1);
            try
            {
                CheckIntPtr(pointer, "blah");
            }
            finally
            {
                protectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [Fact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                CheckIntPtr(IntPtr.Zero, "blah");
            });
        }

        [Fact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                CheckIntPtr(new IntPtr(-1), "blah");
            });
        }

        [Fact]
        private void TestCheckZeroWithZeroResult()
        {
            CheckZero(0, "blah");
        }

        [Fact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Assert.Throws<LibcOperationFailedException>(() => { CheckZero(1, "blah"); });
        }

        [Fact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            CheckZero(0, "blah", new InvalidOperationException());
        }

        [Fact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                CheckZero(1, "blah", new InvalidOperationException());
            });
        }
    }
}
