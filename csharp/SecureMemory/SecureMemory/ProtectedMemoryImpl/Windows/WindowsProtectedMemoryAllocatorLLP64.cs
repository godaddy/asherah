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

        public IntPtr Alloc(ulong length)
        {
            length = AdjustLength(length);

            var result = WindowsInterop.VirtualAlloc(IntPtr.Zero, (UIntPtr)length, AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.PAGE_EXECUTE_READWRITE);
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

            if (!WindowsInterop.VirtualFree(pointer, UIntPtr.Zero, AllocationType.RELEASE))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualFreeEx", 0L, errno);
            }
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptProtectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("CryptProtectMemory", 0L, errno);
            }
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);
        }

        private ulong AdjustLength(ulong length)
        {
            return length % CryptProtect.BLOCKSIZE != 0
                ? ((length / CryptProtect.BLOCKSIZE) + 1) * CryptProtect.BLOCKSIZE
                : length;
        }
    }
}
