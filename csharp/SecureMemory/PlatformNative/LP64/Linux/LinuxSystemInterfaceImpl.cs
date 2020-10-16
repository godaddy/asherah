using System;
using System.Collections.Generic;
using System.Text;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
    internal class LinuxSystemInterfaceImpl : SystemInterface
    {
        private readonly LinuxLibcLP64 libc;

        public LinuxSystemInterfaceImpl(LinuxLibcLP64 libc)
        {
            this.libc = libc;
        }

        public override void CopyMemory(IntPtr source, IntPtr dest, ulong length)
        {
            libc.memcpy(dest, source, length);
        }

        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            libc.bzero(ptr, length);
        }
    }
}
