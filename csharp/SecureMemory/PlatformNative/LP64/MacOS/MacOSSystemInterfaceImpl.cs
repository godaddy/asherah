using System;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums;

namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS
{
    internal class MacOSSystemInterfaceImpl : LibcSystemInterface
    {
        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            LibcLP64.memset_s(ptr, length, 0, length);
        }

        public override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
            if (!AreCoreDumpsGloballyDisabled())
            {
                DisableCoreDumpGlobally();
                if (!AreCoreDumpsGloballyDisabled())
                {
                    throw new SystemException("Failed to disable core dumps");
                }
            }
        }

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }

        // These flags are platform specific in their integer values
        internal override int GetProtReadWrite()
        {
            return (int)(MmapProts.PROT_READ | MmapProts.PROT_WRITE);
        }

        internal override int GetProtRead()
        {
            return (int)MmapProts.PROT_READ;
        }

        internal override int GetProtNoAccess()
        {
            return (int)MmapProts.PROT_NONE;
        }

        internal override int GetPrivateAnonymousFlags()
        {
            return (int)(MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANON);
        }

        internal override int GetMemLockLimit()
        {
            return (int)RlimitResource.RLIMIT_MEMLOCK;
        }
    }
}
