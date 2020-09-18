using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private void CheckBadZero()
        {
            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(10, "CheckBadZero");
            });            
        }
    }
}
