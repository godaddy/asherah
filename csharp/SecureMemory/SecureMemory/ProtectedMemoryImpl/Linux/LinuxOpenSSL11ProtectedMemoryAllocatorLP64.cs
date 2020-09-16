using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    internal class LinuxOpenSSL11ProtectedMemoryAllocatorLP64 : LinuxProtectedMemoryAllocatorLP64, IProtectedMemoryAllocator, IDisposable
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly LinuxOpenSSL11LP64 openSSL11;
        private bool disposedValue;

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(ulong size, int minsize)
            : base((LinuxLibcLP64)new LinuxOpenSSL11LP64())
        {
            openSSL11 = (LinuxOpenSSL11LP64)GetLibc();
            CheckResult(openSSL11.CRYPTO_secure_malloc_init(size, minsize), 1, "CRYPTO_secure_malloc_init");
        }

        ~LinuxOpenSSL11ProtectedMemoryAllocatorLP64()
        {
            Dispose(disposing: false);
        }

        public static bool IsAvailable()
        {
            return LinuxOpenSSL11LP64.IsAvailable();
        }

        public override void ZeroMemory(IntPtr pointer, ulong length)
        {
            // CRYPTO_secure_clear_free includes ZeroMemory functionality
        }

        public override void SetNoAccess(IntPtr pointer, ulong length)
        {
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
        }

        public override void SetReadAccess(IntPtr pointer, ulong length)
        {
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
        }

        public override void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
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

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                CheckResult(openSSL11.CRYPTO_secure_malloc_done(), 1, "CRYPTO_secure_malloc_done");
                disposedValue = true;
            }
        }
    }
}
