using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal sealed class WindowsProtectedMemoryAllocatorVirtualAlloc : WindowsProtectedMemoryAllocatorLLP64
    {
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
