using System;
using System.Diagnostics;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.OpenSSL;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL
{
    internal class LinuxOpenSSL11ProtectedMemoryAllocatorLP64 : LibcProtectedMemoryAllocatorLP64
    {
        private const ulong DefaultHeapSize = 32768;
        private const int DefaultMinimumAllocationSize = 32;
        private readonly ulong encryptedMemoryBlockSize;
        private readonly OpenSSL11LP64 openSSL11;
        private readonly SystemInterface systemInterface;
        private bool disposedValue;

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(IConfiguration configuration)
            : this(configuration, SystemInterface.GetInstance(), new OpenSSL11LP64())
        {
        }

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(IConfiguration configuration, SystemInterface systemInterface)
            : this(configuration, systemInterface, new OpenSSL11LP64())
        {
        }

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(
            IConfiguration configuration,
            SystemInterface systemInterface,
            OpenSSL11LP64 libc)
            : base(systemInterface)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (systemInterface == null)
            {
                throw new ArgumentNullException(nameof(systemInterface));
            }

            openSSL11 = libc;
            this.systemInterface = systemInterface;
            encryptedMemoryBlockSize = systemInterface.GetEncryptedMemoryBlockSize();

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

            systemInterface.GetEncryptedMemoryBlockSize();

            // cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface);
            // blockSize = (ulong)cryptProtectMemory.GetBlockSize();
        }

        ~LinuxOpenSSL11ProtectedMemoryAllocatorLP64()
        {
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Finalizer");
            Dispose(disposing: false);
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

            systemInterface.ProcessEncryptMemory(pointer, length);
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
            length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);
            Debug.WriteLine($"SetReadAccess: New length {length}");

            systemInterface.ProcessDecryptMemory(pointer, length);
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
            length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);
            Debug.WriteLine($"SetReadWriteAccess: New length {length}");

            systemInterface.ProcessDecryptMemory(pointer, length);
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
            length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);

            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Alloc({length})");
            IntPtr protectedMemory = openSSL11.CRYPTO_secure_malloc(length);

            Check.IntPtr(protectedMemory, "CRYPTO_secure_malloc");
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Alloc returned {protectedMemory}");
            try
            {
                SystemInterface.SetNoDump(protectedMemory, length);
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
            length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);

            Check.IntPtr(pointer, "LinuxOpenSSL11ProtectedMemoryAllocatorLP64.Free");

            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: Free({pointer},{length})");
            openSSL11.CRYPTO_secure_clear_free(pointer, length);
        }

        protected override void Dispose(bool disposing)
        {
            Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose({disposing})");
            if (disposing)
            {
                if (!disposedValue)
                {
                    disposedValue = true;
                }
            }
        }
    }
}
