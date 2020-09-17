using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS
{
    /*
     * MacOS protected memory implementation supports:
     *
     * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
     * mlock() - Locked (no swap)
     * setrlimit(RLIMIT_CORE, 0) - Globally disable core dumps
     * madvise(MADV_ZERO_WIRED_PAGES) - Request that the pages are zeroed before deallocation
     */

    internal class MacOSProtectedMemoryAllocatorLP64 : LibcProtectedMemoryAllocatorLP64
    {
        private readonly MacOSLibcLP64 libc;

        public MacOSProtectedMemoryAllocatorLP64()
            : base(new MacOSLibcLP64())
        {
            libc = (MacOSLibcLP64)GetLibc();
            DisableCoreDumpGlobally();
        }

        public MacOSProtectedMemoryAllocatorLP64(MacOSLibcLP64 libc)
            : base(libc)
        {
            this.libc = libc;
        }

        public override void ZeroMemory(IntPtr pointer, ulong length)
        {
            // This differs on different platforms
            // MacOS has memset_s which is standardized and secure
            libc.memset_s(pointer, length, 0, length);
        }

        public override void Dispose()
        {
        }

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }

        // Platform specific blocking memory from core dump
        internal override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
            // MacOS doesn't have madvise(MAP_DONTDUMP) so we have to disable core dumps globally
            if (!AreCoreDumpsGloballyDisabled())
            {
                DisableCoreDumpGlobally();
                if (!AreCoreDumpsGloballyDisabled())
                {
                    throw new SystemException("Failed to disable core dumps");
                }
            }
        }

        // These flags are platform specific in their integer values
        protected override int GetProtRead()
        {
            return (int)MmapProts.PROT_READ;
        }

        protected override int GetProtReadWrite()
        {
            return (int)(MmapProts.PROT_READ | MmapProts.PROT_WRITE);
        }

        protected override int GetProtNoAccess()
        {
            return (int)MmapProts.PROT_NONE;
        }

        protected override int GetPrivateAnonymousFlags()
        {
            return (int)(MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANON);
        }

        protected override int GetMemLockLimit()
        {
            return (int)RlimitResource.RLIMIT_MEMLOCK;
        }
    }
}
