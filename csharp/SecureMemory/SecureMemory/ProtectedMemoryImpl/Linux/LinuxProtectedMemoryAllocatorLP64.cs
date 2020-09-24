using System;
using System.Diagnostics;
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
        private readonly int pageSize = Environment.SystemPageSize;
        private readonly LinuxLibcLP64 libc;

        public LinuxProtectedMemoryAllocatorLP64()
            : base(new LinuxLibcLP64())
        {
            Debug.WriteLine("LinuxProtectedMemoryAllocatorLP64 ctor");
            libc = (LinuxLibcLP64)GetLibc();
        }

        public LinuxProtectedMemoryAllocatorLP64(LinuxLibcLP64 libc)
            : base(libc)
        {
            this.libc = libc;
        }

        public override void Dispose()
        {
        }

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }

        internal override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
            Check.IntPtr(protectedMemory, "SetNoDump");
            if (length == 0)
            {
                throw new Exception("SetNoDump: Invalid length");
            }

            // Calculate the 4KB page aligned pointer for madvise
            long addr = protectedMemory.ToInt64();
            if (addr % pageSize != 0)
            {
                addr -= addr % pageSize;
            }

            IntPtr pagePointer = new IntPtr(addr);

            // Enable selective core dump avoidance
            Check.Zero(libc.madvise(pagePointer, length, (int)Madvice.MADV_DONTDUMP), $"madvise({protectedMemory}, {length}, MADV_DONTDUMP)");
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

        // Platform specific zero memory
        protected override void ZeroMemory(IntPtr pointer, ulong length)
        {
            Check.IntPtr(pointer, "ZeroMemory");
            if (length < 1)
            {
                throw new Exception("ZeroMemory: Invalid length");
            }

            // Glibc bzero doesn't seem to be vulnerable to being optimized away
            // Glibc doesn't seem to have explicit_bzero, memset_s, or memset_explicit
            libc.bzero(pointer, length);
        }
    }
}
