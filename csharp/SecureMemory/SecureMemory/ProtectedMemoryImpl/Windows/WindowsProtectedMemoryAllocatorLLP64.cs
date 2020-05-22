using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator, IDisposable
    {
        private const int PageSize = 4096;
        private IntPtr heap;
        private IntPtr process;
        private bool disposedValue;

        public WindowsProtectedMemoryAllocatorLLP64()
        {
            process = WindowsInterop.GetCurrentProcess();
            heap = WindowsInterop.HeapCreate(0, (UIntPtr)0, (UIntPtr)0);
        }

        ~WindowsProtectedMemoryAllocatorLLP64()
        {
            Dispose(disposing: false);
        }

        public IntPtr Alloc(ulong length)
        {
            var result = WindowsInterop.HeapAlloc(heap, 0, (UIntPtr)Math.Min(length, PageSize));
            if (result == IntPtr.Zero)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("HeapAlloc", (long)result, errno);
            }

            return result;
        }

        public void Free(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.HeapFree(heap, 0, pointer))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("HeapFree", 0L, errno);
            }
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)AllocationProtect.PAGE_NOACCESS, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)AllocationProtect.PAGE_READONLY, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)AllocationProtect.PAGE_READWRITE, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (IntPtr)length);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (heap != IntPtr.Zero)
                {
                    WindowsInterop.HeapDestroy(heap);
                    heap = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }
    }
}
