using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
  internal sealed class WindowsProtectedMemoryAllocatorVirtualAlloc : WindowsProtectedMemoryAllocatorLLP64
  {
    private const int DefaultMaximumWorkingSetSize = 67108860;
    private const int DefaultMinimumWorkingSetSize = 33554430;

    public WindowsProtectedMemoryAllocatorVirtualAlloc(IConfiguration configuration)
    {
      var min = UIntPtr.Zero;
      var max = UIntPtr.Zero;
      var hProcess = WindowsInterop.GetCurrentProcess();
      var result = WindowsInterop.GetProcessWorkingSetSize(hProcess, ref min, ref max);
      if (!result)
      {
        throw new InvalidOperationException("GetProcessWorkingSetSize failed");
      }

      var minConfig = configuration["minimumWorkingSetSize"];
      if (!string.IsNullOrWhiteSpace(minConfig))
      {
        min = new UIntPtr(ulong.Parse(minConfig));
      }
      else
      {
        if (min.ToUInt64() < DefaultMinimumWorkingSetSize)
        {
          min = new UIntPtr(DefaultMinimumWorkingSetSize);
        }
      }

      var maxConfig = configuration["maximumWorkingSetSize"];
      if (!string.IsNullOrWhiteSpace(maxConfig))
      {
        max = new UIntPtr(ulong.Parse(maxConfig));
      }
      else
      {
        if (max.ToUInt64() < DefaultMaximumWorkingSetSize)
        {
          max = new UIntPtr(DefaultMaximumWorkingSetSize);
        }
      }

      result = WindowsInterop.SetProcessWorkingSetSize(hProcess, min, max);
      if (!result)
      {
        throw new InvalidOperationException($"SetProcessWorkingSetSize({min.ToUInt64()},{max.ToUInt64()}) failed");
      }
    }

    public override IntPtr Alloc(ulong length)
    {
      length = AdjustLength(length);

      var result = WindowsInterop.VirtualAlloc(IntPtr.Zero, (UIntPtr)length, AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.PAGE_EXECUTE_READWRITE);
      if (result == IntPtr.Zero || result == InvalidPointer)
      {
        var errno = Marshal.GetLastWin32Error();
        throw new WindowsOperationFailedException("VirtualAlloc", (long)result, errno);
      }

      return result;
    }

    public override void Free(IntPtr pointer, ulong length)
    {
      WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);

      if (!WindowsInterop.VirtualFree(pointer, UIntPtr.Zero, AllocationType.RELEASE))
      {
        var errno = Marshal.GetLastWin32Error();
        throw new WindowsOperationFailedException("VirtualFree", 0L, errno);
      }
    }
  }
}
