using System;
using System.Collections.Generic;
using System.Text;

namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS
{
    internal class MacOSSystemInterfaceImpl : SystemInterface
    {
        private readonly MacOSLibcLP64 libc;

        public MacOSSystemInterfaceImpl(MacOSLibcLP64 libc)
        {
            this.libc = libc;
        }

        public override void CopyMemory(IntPtr source, IntPtr dest, ulong length)
        {
            libc.memcpy(dest, source, length);
        }

        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            libc.memset_s(ptr, length, 0, length);
        }
    }
}
