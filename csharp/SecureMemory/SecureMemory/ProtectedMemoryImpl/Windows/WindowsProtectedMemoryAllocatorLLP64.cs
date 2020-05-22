using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);
        private readonly IntPtr process;

        public WindowsProtectedMemoryAllocatorLLP64()
        {
            process = WindowsInterop.GetCurrentProcess();
        }

        public IntPtr Alloc(ulong length)
        {
            var result = WindowsInterop.VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)length, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.PAGE_EXECUTE_READWRITE);
            if (result == IntPtr.Zero || result == InvalidPointer)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualAllocEx", (long)result, errno);
            }

            return result;
        }

        public void Free(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);

            if (!WindowsInterop.VirtualFreeEx(process, pointer, UIntPtr.Zero, AllocationType.Release))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualFreeEx", 0L, errno);
            }
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_NOACCESS, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_READONLY, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_READWRITE, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0, errno);
            }
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);
        }
    }
}
