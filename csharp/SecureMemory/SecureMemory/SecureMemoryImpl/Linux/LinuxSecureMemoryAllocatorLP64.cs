using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux
{
    /*
     * Linux protected memory implementation supports:
     *
     * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
     * madvise(MADV_DONTDUMP) - Selective core dump avoidance
     */

    internal class LinuxSecureMemoryAllocatorLP64 : LibcSecureMemoryAllocatorLP64
    {
        private readonly int pageSize = Environment.SystemPageSize;

        public override void Dispose()
        {
        }

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }

        internal override void SetNoDump(IntPtr secureMemory, ulong length)
        {
            Check.IntPointer(secureMemory, "SetNoDump");
            if (length == 0)
            {
                throw new SecureMemoryException("SetNoDump: Invalid length");
            }

            // Calculate the 4KB page aligned pointer for madvise
            var addr = secureMemory.ToInt64();
            if (addr % pageSize != 0)
            {
                addr -= addr % pageSize;
            }

            var pagePointer = new IntPtr(addr);

            // Enable selective core dump avoidance
            Check.Zero(LibcLP64.madvise(pagePointer, length, (int)Madvice.MADV_DONTDUMP), $"madvise({secureMemory}, {length}, MADV_DONTDUMP)");
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

        // Platform specific zero memory
        protected override void ZeroMemory(IntPtr pointer, ulong length)
        {
            Check.IntPointer(pointer, "ZeroMemory");
            if (length < 1)
            {
                throw new SecureMemoryException("ZeroMemory: Invalid length");
            }

            // Glibc bzero doesn't seem to be vulnerable to being optimized away
            // Glibc doesn't seem to have explicit_bzero, memset_s, or memset_explicit
            LinuxLibcLP64.bzero(pointer, length);
        }
    }
}
