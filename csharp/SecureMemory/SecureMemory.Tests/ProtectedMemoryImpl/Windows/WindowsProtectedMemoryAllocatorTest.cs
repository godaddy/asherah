using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Windows
{
  [Collection("Logger Fixture collection")]
  public class WindowsProtectedMemoryAllocatorTest : IDisposable
  {
    private static readonly byte[] ZeroBytes = new byte[] { 0, 0, 0, 0 };
    private readonly WindowsProtectedMemoryAllocatorVirtualAlloc windowsProtectedMemoryAllocator;

    public WindowsProtectedMemoryAllocatorTest()
    {
      Trace.Listeners.Clear();
      var consoleListener = new ConsoleTraceListener();
      Trace.Listeners.Add(consoleListener);

      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { "minimumWorkingSetSize", "33554430"},
                { "maximumWorkingSetSize", "67108860"},
            }).Build();

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        windowsProtectedMemoryAllocator = new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration);
      }
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);
      windowsProtectedMemoryAllocator?.Dispose();
    }

    [Fact]
    private void TestAllocSuccess()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Test only runs on Windows");

      var pointer = windowsProtectedMemoryAllocator.Alloc(1);
      try
      {
        // just do some sanity checks
        Marshal.WriteByte(pointer, 0, 1);
        Assert.Equal(1, Marshal.ReadByte(pointer, 0));
      }
      finally
      {
        windowsProtectedMemoryAllocator.Free(pointer, 1);
      }
    }

    [Fact]
    private void TestZeroMemory()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Test only runs on Windows");

      byte[] origValue = { 1, 2, 3, 4 };
      var length = (ulong)origValue.Length;

      var pointer = windowsProtectedMemoryAllocator.Alloc(length);

      try
      {
        Marshal.Copy(origValue, 0, pointer, (int)length);

        var retValue = new byte[length];
        Marshal.Copy(pointer, retValue, 0, (int)length);
        Assert.Equal(origValue, retValue);

        WindowsProtectedMemoryAllocatorLLP64.ZeroMemory(pointer, length);
        Marshal.Copy(pointer, retValue, 0, (int)length);
        Assert.Equal(ZeroBytes, retValue);
      }
      finally
      {
        windowsProtectedMemoryAllocator.Free(pointer, length);
      }
    }
  }
}
