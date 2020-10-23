using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    internal class WindowsSystemInterfaceImpl : SystemInterface
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);
        private readonly IntPtr hProcess;

        public WindowsSystemInterfaceImpl()
        {
            hProcess = WindowsInterop.GetCurrentProcess();
        }

        public override void CopyMemory(IntPtr source, IntPtr dest, ulong length)
        {
            WindowsInterop.CopyMemory(dest, source, UIntPtr.Add(UIntPtr.Zero, (int)length));
        }

        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            WindowsInterop.ZeroMemory(ptr, UIntPtr.Add(UIntPtr.Zero, (int)length));
        }

        public override bool AreCoreDumpsGloballyDisabled()
        {
            return false;
        }

        public override bool DisableCoreDumpGlobally()
        {
            return false;
        }

        public override void SetNoAccess(IntPtr pointer, ulong length)
        {
            var result = WindowsInterop.VirtualProtectEx(
                hProcess,
                pointer,
                (UIntPtr)length,
                (uint)MemoryProtection.PAGE_NOACCESS,
                out uint _);

            if (!result)
            {
                throw new WindowsOperationFailedException("VirtualProtectEx", 0, Marshal.GetLastWin32Error());
            }
        }

        public override void SetReadAccess(IntPtr pointer, ulong length)
        {
            var result = WindowsInterop.VirtualProtectEx(
                hProcess,
                pointer,
                (UIntPtr)length,
                (uint)MemoryProtection.PAGE_READONLY,
                out uint _);

            if (!result)
            {
                throw new WindowsOperationFailedException("VirtualProtectEx", 0, Marshal.GetLastWin32Error());
            }
        }

        public override void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            var result = WindowsInterop.VirtualProtectEx(
                hProcess,
                pointer,
                (UIntPtr)length,
                (uint)MemoryProtection.PAGE_READWRITE,
                out uint _);

            if (!result)
            {
                throw new WindowsOperationFailedException("VirtualProtectEx", 0, Marshal.GetLastWin32Error());
            }
        }

        public override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
        }

        public override IntPtr PageAlloc(ulong length)
        {
            var result = WindowsInterop.VirtualAlloc(IntPtr.Zero, (UIntPtr)length, AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.PAGE_READWRITE);
            if (result == IntPtr.Zero || result == InvalidPointer)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("VirtualAlloc", (long)result, errno);
            }

            return result;
        }

        public override void PageFree(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualFree(pointer, UIntPtr.Zero, AllocationType.RELEASE))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("VirtualFree", 0L, errno);
            }
        }

        public override void LockMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualLock(pointer, (UIntPtr)length))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("VirtualLock", 0L, errno);
            }
        }

        public override void UnlockMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualUnlock(pointer, (UIntPtr)length))
            {
                var errno = Marshal.GetLastWin32Error();
                if (errno == (int)VirtualUnlockErrors.ERROR_NOT_LOCKED)
                {
                    return;
                }

                throw new WindowsOperationFailedException("VirtualUnlock", 0L, errno);
            }
        }

        public override ulong GetMemoryLockLimit()
        {
            UIntPtr min = UIntPtr.Zero;
            UIntPtr max = UIntPtr.Zero;
            var result = WindowsInterop.GetProcessWorkingSetSize(hProcess, ref min, ref max);
            if (!result)
            {
                throw new Exception("GetProcessWorkingSetSize failed");
            }

            return (ulong)max;
        }

        public override void SetMemoryLockLimit(ulong limit)
        {
            UIntPtr min = UIntPtr.Zero;
            UIntPtr max = UIntPtr.Zero;
            var result = WindowsInterop.GetProcessWorkingSetSize(hProcess, ref min, ref max);
            if (!result)
            {
                throw new Exception("GetProcessWorkingSetSize failed");
            }

            if (limit < (ulong)max)
            {
                // Already sufficiently large limit
                return;
            }

            max = (UIntPtr)limit;

            result = WindowsInterop.SetProcessWorkingSetSize(hProcess, min, max);
            if (!result)
            {
                throw new Exception($"SetProcessWorkingSetSize({min.ToUInt64()},{max.ToUInt64()}) failed");
            }
        }
    }
}
