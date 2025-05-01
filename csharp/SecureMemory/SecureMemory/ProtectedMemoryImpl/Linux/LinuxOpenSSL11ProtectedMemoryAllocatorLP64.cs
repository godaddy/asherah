using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    internal class LinuxOpenSSL11ProtectedMemoryAllocatorLP64 : LinuxProtectedMemoryAllocatorLP64
    {
        private const ulong DefaultHeapSize = 32768;
        private const int DefaultMinimumAllocationSize = 32;
        private readonly ulong blockSize;
        private LinuxOpenSSL11LP64 openSSL11;
        private OpenSSLCryptProtectMemory cryptProtectMemory;
        private bool disposedValue;

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(IConfiguration configuration)
            : base(new LinuxOpenSSL11LP64())
        {
            openSSL11 = (LinuxOpenSSL11LP64)GetLibc();

            ulong heapSize;
            var heapSizeConfig = configuration["heapSize"];
            if (!string.IsNullOrWhiteSpace(heapSizeConfig))
            {
                heapSize = ulong.Parse(heapSizeConfig);
            }
            else
            {
                heapSize = DefaultHeapSize;
            }

            int minimumAllocationSize;
            var minimumAllocationSizeConfig = configuration["minimumAllocationSize"];
            if (!string.IsNullOrWhiteSpace(minimumAllocationSizeConfig))
            {
                minimumAllocationSize = int.Parse(minimumAllocationSizeConfig);
            }
            else
            {
                minimumAllocationSize = DefaultMinimumAllocationSize;
            }

            Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorLP64: openSSL11 is not null");

            Debug.WriteLine($"*** LinuxOpenSSL11ProtectedMemoryAllocatorLP64: CRYPTO_secure_malloc_init ***");
            Check.Result(openSSL11.CRYPTO_secure_malloc_init(heapSize, minimumAllocationSize), 1, "CRYPTO_secure_malloc_init");

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
            catch (Exception e)
            {
                openSSL11.CRYPTO_secure_free(protectedMemory);
                throw new SecureMemoryAllocationFailedException("Failed to set no dump on protected memory", e);
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
