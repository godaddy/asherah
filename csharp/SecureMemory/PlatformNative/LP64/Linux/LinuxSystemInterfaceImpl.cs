using System;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
    internal class LinuxSystemInterfaceImpl : LibcSystemInterface
    {
        private bool globallyDisabledCoreDumps;

        public override void ZeroMemory(IntPtr ptr, ulong length)
        {
            LibcLP64.bzero(ptr, length);
        }

        public override bool AreCoreDumpsGloballyDisabled()
        {
            return globallyDisabledCoreDumps;
        }

        public override bool DisableCoreDumpGlobally()
        {
            try
            {
                Check.Zero(LibcLP64.setrlimit(GetRlimitCoreResource(), rlimit.Zero()), "setrlimit(RLIMIT_CORE)");
                globallyDisabledCoreDumps = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void SetNoDump(IntPtr protectedMemory, ulong length)
        {
            Check.IntPtr(protectedMemory, "SetNoDump");
            if (length == 0)
            {
                throw new Exception("SetNoDump: Invalid length");
            }

            // Calculate the 4KB page aligned pointer for Linux madvise
            long addr = protectedMemory.ToInt64();
            if (addr % PageSize != 0)
            {
                addr -= addr % PageSize;
            }

            IntPtr pagePointer = new IntPtr(addr);

            // Enable selective core dump avoidance
            Check.Zero(LibcLP64.madvise(pagePointer, length, (int)Madvice.MADV_DONTDUMP), $"madvise({protectedMemory}, {length}, MADV_DONTDUMP)");
        }

        public override void SetMemoryLockLimit(ulong limit)
        {
            throw new NotImplementedException();
        }

        public override ulong GetEncryptedMemoryBlockSize()
        {
            throw new NotImplementedException();
        }

        public override void ProcessEncryptMemory(IntPtr pointer, ulong length)
        {
            throw new NotImplementedException();
        }

        public override void ProcessDecryptMemory(IntPtr pointer, ulong length)
        {
            throw new NotImplementedException();
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

        internal override int GetRlimitCoreResource()
        {
            return (int)RlimitResource.RLIMIT_CORE;
        }
    }
}
