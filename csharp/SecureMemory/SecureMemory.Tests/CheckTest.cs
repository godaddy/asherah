using System;
using GoDaddy.Asherah.SecureMemory.Libc;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
  [Collection("Logger Fixture collection")]
  public class CheckTest
  {
    [Fact]
    private void CheckIntPtr()
    {
      var ptr = IntPtr.Add(IntPtr.Zero, 1000);
      Check.ValidatePointer(ptr, "CheckIntPtr");
    }

    [Fact]
    private void CheckBadIntPtr()
    {
      Assert.Throws<LibcOperationFailedException>(() =>
      {
        var ptr = IntPtr.Zero;
        Check.ValidatePointer(ptr, "CheckBadIntPtr");
      });
    }

    [Fact]
    private void CheckInvalidIntPtr()
    {
      Assert.Throws<LibcOperationFailedException>(() =>
      {
        var ptr = Check.InvalidPointer;
        Check.ValidatePointer(ptr, "CheckBadIntPtr");
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
