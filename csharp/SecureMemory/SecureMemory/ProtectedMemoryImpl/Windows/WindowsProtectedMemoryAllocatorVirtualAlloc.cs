using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal sealed class WindowsProtectedMemoryAllocatorVirtualAlloc : WindowsProtectedMemoryAllocatorLLP64
    {
        public WindowsProtectedMemoryAllocatorVirtualAlloc(IConfiguration configuration)
        {
            UIntPtr min = UIntPtr.Zero;
            UIntPtr max = UIntPtr.Zero;
            IntPtr hProcess = WindowsInterop.GetCurrentProcess();
            var result = WindowsInterop.GetProcessWorkingSetSize(hProcess, ref min, ref max);
            if (!result)
            {
                throw new Exception("GetProcessWorkingSetSize failed");
            }

            var minConfig = configuration["minimumWorkingSetSize"];
            min = !string.IsNullOrWhiteSpace(minConfig) ?
                new UIntPtr(ulong.Parse(minConfig)) :
                new UIntPtr(min.ToUInt64() * 16);

            var maxConfig = configuration["maximumWorkingSetSize"];
            max = !string.IsNullOrWhiteSpace(maxConfig) ?
                new UIntPtr(ulong.Parse(maxConfig)) :
                new UIntPtr(max.ToUInt64() * 32);

            result = WindowsInterop.SetProcessWorkingSetSize(hProcess, min, max);
            if (!result)
            {
                throw new Exception("SetProcessWorkingSetSize failed");
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
