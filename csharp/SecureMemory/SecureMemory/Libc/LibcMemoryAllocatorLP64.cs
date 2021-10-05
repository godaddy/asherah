using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.Libc
{
    internal abstract class LibcMemoryAllocatorLP64 : ISecureMemoryAllocator
    {
        private LibcLP64 libc;
        private bool globallyDisabledCoreDumps;

        protected LibcMemoryAllocatorLP64(LibcLP64 libc)
        {
            this.libc = libc ?? throw new ArgumentNullException(nameof(libc));
        }

        // Implementation order of preference:
        // memset_s (standards)
        // explicit_bzero (BSD)
        // SecureZeroMemory (Windows)
        // bzero (Linux, same guarantees as explicit_bzero)
        public virtual void SetNoAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtNoAccess()), "mprotect(PROT_NONE)");
        }

        public virtual void SetReadAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtRead()), "mprotect(PROT_READ)");
        }

        public abstract IntPtr Alloc(ulong length);

        public abstract void Free(IntPtr pointer, ulong length);

        public virtual void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtReadWrite()), "mprotect(PROT_READ|PROT_WRITE)");
        }

        public abstract void Dispose();

        internal abstract int GetRlimitCoreResource();

        // ************************************
        // Core dumps
        // ************************************
        internal abstract void SetNoDump(IntPtr secureMemory, ulong length);

        internal bool AreCoreDumpsGloballyDisabled()
        {
            return globallyDisabledCoreDumps;
        }

        internal void DisableCoreDumpGlobally()
        {
            Check.Zero(libc.setrlimit(GetRlimitCoreResource(), rlimit.Zero()), "setrlimit(RLIMIT_CORE)");

            globallyDisabledCoreDumps = true;
        }

        // ************************************
        // Memory protection
        // ************************************
        internal abstract int GetProtRead();

        internal abstract int GetProtReadWrite();

        internal abstract int GetProtNoAccess();

        internal abstract int GetPrivateAnonymousFlags();

        protected abstract void ZeroMemory(IntPtr pointer, ulong length);

        protected LibcLP64 GetLibc()
        {
            return libc;
        }
    }
}
