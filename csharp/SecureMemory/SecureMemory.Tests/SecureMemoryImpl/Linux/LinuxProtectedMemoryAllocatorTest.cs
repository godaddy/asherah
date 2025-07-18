using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl.Linux
{
  [Collection("Logger Fixture collection")]
  public class LinuxProtectedMemoryAllocatorTest : IDisposable
  {
    private LinuxSecureMemoryAllocatorLP64 linuxProtectedMemoryAllocator;

    public LinuxProtectedMemoryAllocatorTest()
    {
      Trace.Listeners.Clear();
      var consoleListener = new ConsoleTraceListener();
      Trace.Listeners.Add(consoleListener);

      Debug.WriteLine("LinuxProtectedMemoryAllocatorTest ctor");
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        linuxProtectedMemoryAllocator = new LinuxSecureMemoryAllocatorLP64();
      }
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);
      Debug.WriteLine("LinuxProtectedMemoryAllocatorTest.Dispose");
      linuxProtectedMemoryAllocator?.Dispose();
    }

    [Fact]
    private void TestSetNoDumpInvalidLength()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      var fakeValidPointer = IntPtr.Add(IntPtr.Zero, 1);
      Assert.Throws<Exception>(() => linuxProtectedMemoryAllocator.SetNoDump(fakeValidPointer, 0));
    }

    [Fact]
    private void TestGetResourceCore()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Assert.Equal(4, linuxProtectedMemoryAllocator.GetRlimitCoreResource());
    }

    [Fact]
    private void TestAllocFree()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      byte[] origValue = { 1, 2, 3, 4 };
      var length = (ulong)origValue.Length;

      var pointer = linuxProtectedMemoryAllocator.Alloc(length);

      try
      {
        Marshal.Copy(origValue, 0, pointer, (int)length);

        var retValue = new byte[length];
        Marshal.Copy(pointer, retValue, 0, (int)length);
        Assert.Equal(origValue, retValue);
      }
      finally
      {
        linuxProtectedMemoryAllocator.Free(pointer, length);
      }
    }
  }
}
