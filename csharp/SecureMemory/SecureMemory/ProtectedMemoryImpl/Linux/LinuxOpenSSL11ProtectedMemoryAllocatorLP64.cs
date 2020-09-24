using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Schema;
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
        private readonly ulong blockSize;
        private LinuxOpenSSL11LP64 openSSL11;
        private OpenSSLCryptProtectMemory cryptProtectMemory;
        private bool disposedValue;

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(ulong size, int minsize)
            : base((LinuxLibcLP64)new LinuxOpenSSL11LP64())
        {
            openSSL11 = (LinuxOpenSSL11LP64)GetLibc();
            if (openSSL11 == null)
            {
                throw new Exception("GetLibc returned null object for openSSL11");
            }

            Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorLP64: openSSL11 is not null");

            Debug.WriteLine($"*** LinuxOpenSSL11ProtectedMemoryAllocatorLP64: CRYPTO_secure_malloc_init ***");
            Check.Result(openSSL11.CRYPTO_secure_malloc_init(size, minsize), 1, "CRYPTO_secure_malloc_init");

            cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", this);
            blockSize = (ulong)cryptProtectMemory.GetBlockSize();
        }

        ~LinuxOpenSSL11ProtectedMemoryAllocatorLP64()
        {
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Finalizer");
            Dispose(disposing: false);
        }

        public static bool IsAvailable()
        {
            return LinuxOpenSSL11LP64.IsAvailable();
        }

        public override void SetNoAccess(IntPtr pointer, ulong length)
        {
            if (disposedValue)
            {
                throw new Exception("Called SetNoAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64");
            }

            Check.IntPtr(pointer, "SetNoAccess");

            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            // NOTE: No rounding for encrypt!
            Debug.WriteLine($"SetNoAccess: Length {length}");

            cryptProtectMemory.CryptProtectMemory(pointer, (int)length);
        }

        public override void SetReadAccess(IntPtr pointer, ulong length)
        {
            if (disposedValue)
            {
                throw new Exception("Called SetReadAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64");
            }

            Check.IntPtr(pointer, "SetReadAccess");

            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            // Round up allocation size to nearest block size
            Debug.WriteLine($"SetReadAccess: Rounding length {length} to nearest blocksize");
            length = (length + (blockSize - 1)) & ~(blockSize - 1);
            Debug.WriteLine($"SetReadAccess: New length {length}");

            cryptProtectMemory.CryptUnprotectMemory(pointer, (int)length);
        }

        public override void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            if (disposedValue)
            {
                throw new Exception("Called SetReadWriteAccess on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64");
            }

            Check.IntPtr(pointer, "SetReadWriteAccess");

            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            // Round up allocation size to nearest block size
            Debug.WriteLine($"SetReadWriteAccess: Rounding length {length} to nearest blocksize");
            length = (length + (blockSize - 1)) & ~(blockSize - 1);
            Debug.WriteLine($"SetReadWriteAccess: New length {length}");

            cryptProtectMemory.CryptUnprotectMemory(pointer, (int)length);
        }

        // ************************************
        // alloc / free
        // ************************************
        public override IntPtr Alloc(ulong length)
        {
            if (disposedValue)
            {
                throw new Exception("Called Alloc on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64");
            }

            // Round up allocation size to nearest block size
            Debug.WriteLine($"SetReadWriteAccess: Rounding length {length} to nearest blocksize");
            length = (length + (blockSize - 1)) & ~(blockSize - 1);

            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Alloc({length})");
            IntPtr protectedMemory = openSSL11.CRYPTO_secure_malloc(length);

            Check.IntPtr(protectedMemory, "CRYPTO_secure_malloc");
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Alloc returned {protectedMemory}");
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
            if (disposedValue)
            {
                throw new Exception("Called Free on disposed LinuxOpenSSL11ProtectedMemoryAllocatorLP64");
            }

            // Round up allocation size to nearest block size
            length = (length + (blockSize - 1)) & ~(blockSize - 1);

            Check.IntPtr(pointer, "LinuxOpenSSL11ProtectedMemoryAllocatorLP64.Free");

            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Free({pointer},{length})");
            openSSL11.CRYPTO_secure_clear_free(pointer, length);
        }

        public override void Dispose()
        {
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Dispose");
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose({disposing})");
            if (disposing)
            {
                if (!disposedValue)
                {
                    disposedValue = true;
                    cryptProtectMemory.Dispose();
                }
            }
        }

        protected override void ZeroMemory(IntPtr pointer, ulong length)
        {
            // CRYPTO_secure_clear_free includes ZeroMemory functionality
        }
    }
}
