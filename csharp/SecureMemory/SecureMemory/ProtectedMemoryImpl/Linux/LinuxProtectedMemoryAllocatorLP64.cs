using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    /*
     * Linux protected memory implementation supports:
     *
     * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
     * mlock() - Locked (no swap)
     * madvise(MADV_DONTDUMP) - Selective core dump avoidance
     */

    // ReSharper disable once InconsistentNaming
    internal class LinuxProtectedMemoryAllocatorLP64 : LibcProtectedMemoryAllocatorLP64
    {
        private readonly LinuxLibcLP64 libc;

        public LinuxProtectedMemoryAllocatorLP64()
            : base(new LinuxLibcLP64())
        {
            libc = (LinuxLibcLP64)GetLibc();
        }

        public LinuxProtectedMemoryAllocatorLP64(LinuxLibcLP64 libc)
            : base(libc)
        {
            this.libc = libc;
        }

        // Platform specific zero memory
        public override void ZeroMemory(IntPtr pointer, ulong length)
        {
            // Glibc bzero doesn't seem to be vulnerable to being optimized away
            // Glibc doesn't seem to have explicit_bzero, memset_s, or memset_explicit
            libc.bzero(pointer, length);
        }

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }

        internal override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
            // Enable selective core dump avoidance
            CheckZero(libc.madvise(protectedMemory, length, (int)Madvice.MADV_DONTDUMP), "madvise(MADV_DONTDUMP)");
        }

        // These flags are platform specific in their integer values
        protected override int GetProtReadWrite()
        {
            return (int)(MmapProts.PROT_READ | MmapProts.PROT_WRITE);
        }

        protected override int GetProtRead()
        {
            return (int)MmapProts.PROT_READ;
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
