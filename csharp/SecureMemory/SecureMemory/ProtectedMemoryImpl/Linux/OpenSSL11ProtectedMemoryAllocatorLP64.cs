using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.OpenSSL11;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    internal class OpenSSL11ProtectedMemoryAllocatorLP64 : LinuxProtectedMemoryAllocatorLP64, IProtectedMemoryAllocator
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly LinuxOpenSSL11LP64 openSSL11;

        private bool globallyDisabledCoreDumps = false;

        protected OpenSSL11ProtectedMemoryAllocatorLP64(LinuxOpenSSL11LP64 openSSL11)
        {
            this.openSSL11 = openSSL11;
        }

        // Implementation order of preference:
        // memset_s (standards)
        // explicit_bzero (BSD)
        // SecureZeroMemory (Windows)
        // bzero (Linux, same guarantees as explicit_bzero)
        public override void SetNoAccess(IntPtr pointer, ulong length)
        {
            //Per page-protections aren't possible with the OpenSSL secure heap implementation
        }

        public override void SetReadAccess(IntPtr pointer, ulong length)
        {
            //Per page-protections aren't possible with the OpenSSL secure heap implementation
        }

        public override void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            //Per page-protections aren't possible with the OpenSSL secure heap implementation
        }

        // ************************************
        // alloc / free
        // ************************************
        public override IntPtr Alloc(ulong length)
        {
            openSSL11.getrlimit(GetMemLockLimit(), out var rlim);
            if (rlim.rlim_max != rlimit.UNLIMITED && rlim.rlim_max < length)
            {
                throw new MemoryLimitException(
                    $"Requested MemLock length exceeds resource limit max of {rlim.rlim_max}");
            }

            IntPtr protectedMemory = openSSL11.CRYPTO_secure_malloc(length);

            CheckIntPtr(protectedMemory, "CRYPTO_secure_malloc");
            try
            {
                SetNoDump(protectedMemory, length);
            }
            catch (Exception)
            {
                openSSL11.CRYPTO_secure_free(protectedMemory);
                throw;
            }

            return protectedMemory;
        }

        public override void Free(IntPtr pointer, ulong length)
        {
            openSSL11.CRYPTO_secure_clear_free(pointer, length);
        }
    }
}
