#define USE_VIRTUALALLOC
using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator
#if USE_HEAPALLOC
        , IDisposable
#endif
    {
        private const ulong PageSize = 4096;
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);
        private readonly IntPtr process;
#if USE_HEAPALLOC
        private IntPtr heap;
        private bool disposedValue;
#endif

        public WindowsProtectedMemoryAllocatorLLP64()
        {
            process = WindowsInterop.GetCurrentProcess();
#if USE_HEAPALLOC
            heap = WindowsInterop.HeapCreate(0, (UIntPtr)0, (UIntPtr)0);
#endif
        }

#if USE_HEAPALLOC
        ~WindowsProtectedMemoryAllocatorLLP64()
        {
            Dispose(disposing: false);
        }
#endif

        public IntPtr Alloc(ulong length)
        {
#if MINIMUM_PAGE_SIZE
            length = Math.Min(length, PageSize);
#endif
#if USE_GLOBALALLOC
            var result = WindowsInterop.GlobalAlloc(0, (UIntPtr)length);
#elif USE_VIRTUALALLOC
            var result = WindowsInterop.VirtualAllocEx(process, IntPtr.Zero, (IntPtr)length, AllocationType.Commit, MemoryProtection.PAGE_EXECUTE_READWRITE);
#elif USE_HEAPALLOC
            var result = WindowsInterop.HeapAlloc(heap, 0, (UIntPtr)length);
#else
            #error No Windows unmanaged heap allocation strategy selected
#endif
            if (result == IntPtr.Zero || result == InvalidPointer)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("Alloc", (long)result, errno);
            }

            return result;
        }

        public void Free(IntPtr pointer, ulong length)
        {
            bool result;
            WindowsInterop.ZeroMemory(pointer, (IntPtr)length);
#if USE_GLOBALALLOC
            result = WindowsInterop.GlobalFree(pointer) != IntPtr.Zero;
#elif USE_VIRTUALALLOC
            result = WindowsInterop.VirtualFreeEx(process, pointer, 0, AllocationType.Release);
#elif USE_HEAPALLOC
            result = WindowsInterop.HeapFree(heap, 0, pointer)
#endif
            if (!result)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("Free", 0L, errno);
            }
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
#if !USE_GLOBALALLOC && !USE_HEAPALLOC
            // GlobalAlloc/HeapAlloc can't handle restricted page permissions
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_NOACCESS, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
#endif
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
#if !USE_GLOBALALLOC && !USE_HEAPALLOC
            // GlobalAlloc/HeapAlloc can't handle restricted page permissions
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_READONLY, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
#endif
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualProtectEx(process, pointer, (UIntPtr)length, (uint)MemoryProtection.PAGE_READWRITE, out var _))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("VirtualProtectEx", 0L, errno);
            }
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (IntPtr)length);
        }
#if USE_HEAPALLOC

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
#endif
    }
}
