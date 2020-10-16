using System;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    internal class WindowsSystemInterfaceImpl : SystemInterface
    {
        public override void CopyMemory(IntPtr source, IntPtr dest, ulong length)
        {
            WindowsInterop.CopyMemory(dest, source, UIntPtr.Add(UIntPtr.Zero, (int)length));
        }

        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            WindowsInterop.ZeroMemory(ptr, UIntPtr.Add(UIntPtr.Zero, (int)length));
        }
    }
}
