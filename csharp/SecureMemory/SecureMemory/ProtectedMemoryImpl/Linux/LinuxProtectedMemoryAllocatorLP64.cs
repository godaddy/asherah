using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative;
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
            : this(new LinuxLibcLP64(), SystemInterface.GetInstance())
        {
        }

        public LinuxProtectedMemoryAllocatorLP64(SystemInterface systemInterface)
            : base(new LinuxLibcLP64(), systemInterface)
        {
            Debug.WriteLine("LinuxProtectedMemoryAllocatorLP64 ctor");
            libc = (LinuxLibcLP64)GetLibc();
            if (libc == null)
            {
                throw new InvalidOperationException("LinuxProtectedMemoryAllocatorLP64: GetLibc returned null");
            }
        }

        public LinuxProtectedMemoryAllocatorLP64(LinuxLibcLP64 libc, SystemInterface systemInterface)
            : base(libc, systemInterface)
        {
            this.libc = libc ?? throw new ArgumentNullException(nameof(libc));
            if (systemInterface == null)
            {
                throw new ArgumentNullException(nameof(libc));
            }
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
    }
}
