using System;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class CheckTest
    {
        [Fact]
        private void CheckIntPtr()
        {
            IntPtr ptr = IntPtr.Add(IntPtr.Zero, 1000);
            Check.IntPtr(ptr, "CheckIntPtr");
        }

        [Fact]
        private void CheckBadIntPtr()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                IntPtr ptr = IntPtr.Zero;
                Check.IntPtr(ptr, "CheckBadIntPtr");
            });
        }

        [Fact]
        private void CheckInvalidIntPtr()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                IntPtr ptr = Check.InvalidPointer;
                Check.IntPtr(ptr, "CheckBadIntPtr");
            });
        }

        [Fact]
        private void CheckResult()
        {
            Check.Result(10, 10, "CheckResult");
        }

        [Fact]
        private void CheckBadResult()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Result(10, 20, "CheckBadResult");
            });
        }

        [Fact]
        private void CheckZero()
        {
            Check.Zero(0, "CheckZero");
        }

        [Fact]
        private void CheckZeroException()
        {
            var exception = Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(10, "CheckBadZero", new Exception("Exception in progress to set as inner exception"));
            });

            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        private void CheckBadZero()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(10, "CheckBadZero");
            });
        }
    }
}
