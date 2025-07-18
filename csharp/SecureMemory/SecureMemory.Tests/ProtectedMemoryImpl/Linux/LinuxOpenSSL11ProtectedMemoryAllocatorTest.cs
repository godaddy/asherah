using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Linux
{
  [Collection("Logger Fixture collection")]
  public class LinuxOpenSSL11ProtectedMemoryAllocatorTest : IDisposable
  {
    private LinuxOpenSSL11ProtectedMemoryAllocatorLP64 linuxOpenSSL11ProtectedMemoryAllocatorLP64;
    private IConfiguration configuration;

    public LinuxOpenSSL11ProtectedMemoryAllocatorTest()
    {
      Trace.Listeners.Clear();
      var consoleListener = new ConsoleTraceListener();
      Trace.Listeners.Add(consoleListener);

      configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
            }).Build();

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
        linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);
      }
    }

    public void Dispose()
    {
      GC.SuppressFinalize(this);
      Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorTest.Dispose");
      linuxOpenSSL11ProtectedMemoryAllocatorLP64?.Dispose();
    }

    [Fact]
    private void TestGetResourceCore()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest TestGetResourceCore");
      Assert.Equal(4, linuxOpenSSL11ProtectedMemoryAllocatorLP64.GetRlimitCoreResource());
      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest TestGetResourceCore End");
    }

    [Fact]
    private void TestAllocFree()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      byte[] origValue = { 1, 2, 3, 4 };
      var length = (ulong)origValue.Length;

      var pointer = linuxOpenSSL11ProtectedMemoryAllocatorLP64.Alloc(length);

      try
      {
        Marshal.Copy(origValue, 0, pointer, (int)length);

        var retValue = new byte[length];
        Marshal.Copy(pointer, retValue, 0, (int)length);
        Assert.Equal(origValue, retValue);
      }
      finally
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.Free(pointer, length);
      }
    }

    [Fact]
    private void TestSetNoAccessAfterDispose()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");

      linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);

      linuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

      var exception = Assert.Throws<Exception>(() =>
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.SetNoAccess(new IntPtr(-1), 0);
      });
      Assert.Equal("Called SetNoAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64", exception.Message);
    }

    [Fact]
    private void TestReadAccessAfterDispose()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");

      linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);

      linuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

      var exception = Assert.Throws<Exception>(() =>
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.SetReadAccess(new IntPtr(-1), 0);
      });
      Assert.Equal("Called SetReadAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64", exception.Message);
    }

    [Fact]
    private void TestReadWriteAccessAfterDispose()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");

      linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);

      linuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

      var exception = Assert.Throws<Exception>(() =>
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.SetReadWriteAccess(new IntPtr(-1), 0);
      });
      Assert.Equal("Called SetReadWriteAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64", exception.Message);
    }

    [Fact]
    private void TestAllocAfterDispose()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");

      linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);

      linuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

      var exception = Assert.Throws<Exception>(() =>
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.Alloc(0);
      });
      Assert.Equal("Called Alloc on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64", exception.Message);
    }

    [Fact]
    private void TestFreeAfterDispose()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
      linuxOpenSSL11ProtectedMemoryAllocatorLP64 = new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);

      linuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

      var exception = Assert.Throws<Exception>(() =>
      {
        linuxOpenSSL11ProtectedMemoryAllocatorLP64.Free(new IntPtr(-1), 0);
      });
      Assert.Equal("Called Free on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64", exception.Message);
    }
  }
}
