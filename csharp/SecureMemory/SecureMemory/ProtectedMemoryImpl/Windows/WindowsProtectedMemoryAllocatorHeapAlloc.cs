using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal sealed class WindowsProtectedMemoryAllocatorHeapAlloc : WindowsProtectedMemoryAllocatorLLP64, IDisposable
    {
        private IntPtr heap;

        public WindowsProtectedMemoryAllocatorHeapAlloc()
        {
            heap = WindowsInterop.HeapCreate(0, (UIntPtr)0, (UIntPtr)0);
        }

        ~WindowsProtectedMemoryAllocatorHeapAlloc()
        {
            ReleaseUnmanagedResources();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public override IntPtr Alloc(ulong length)
        {
            length = AdjustLength(length);

            var result = WindowsInterop.HeapAlloc(heap, 0, (UIntPtr)length);
            if (result == IntPtr.Zero || result == InvalidPointer)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("HeapAlloc", (long)result, errno);
            }

            return result;
        }

        public override void Free(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);

            if (!WindowsInterop.HeapFree(heap, 0, pointer))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("HeapFree", 0L, errno);
            }
        }

        private void ReleaseUnmanagedResources()
        {
            if (heap != IntPtr.Zero && heap != InvalidPointer)
            {
                if (!WindowsInterop.HeapDestroy(heap))
                {
                    var errno = Marshal.GetLastWin32Error();
                    throw new LibcOperationFailedException("HeapDestroy", 0L, errno);
                }

                heap = IntPtr.Zero;
            }
        }
    }
}
