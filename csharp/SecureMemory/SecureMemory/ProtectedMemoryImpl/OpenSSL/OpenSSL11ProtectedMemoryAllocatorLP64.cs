using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL
{
    internal class OpenSSL11ProtectedMemoryAllocatorLP64 : IProtectedMemoryAllocator
    {
        private const ulong DefaultHeapSize = 32768;
        private const int DefaultMinimumAllocationSize = 32;
        private readonly ulong encryptedMemoryBlockSize;
        private readonly IOpenSSLCrypto openSSL11;
        private readonly SystemInterface systemInterface;
        private bool disposedValue;

        public OpenSSL11ProtectedMemoryAllocatorLP64(
            IConfiguration configuration,
            SystemInterface systemInterface,
            IOpenSSLCrypto openSSL11 = null)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (openSSL11 != null)
            {
                this.openSSL11 = openSSL11;
            }
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    this.openSSL11 = new OpenSSLCryptoWindows(configuration);
                }
                else
                {
                    this.openSSL11 = new OpenSSLCryptoLibc(configuration);
                }
            }

            if (this.openSSL11 == null)
            {
                throw new ArgumentNullException(nameof(openSSL11));
            }

            this.systemInterface = systemInterface ?? throw new ArgumentNullException(nameof(systemInterface));

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
            Check.Result(this.openSSL11.CRYPTO_secure_malloc_init(heapSize, minimumAllocationSize), 1, "CRYPTO_secure_malloc_init");

            systemInterface.GetEncryptedMemoryBlockSize();

            // cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface);
            // blockSize = (ulong)cryptProtectMemory.GetBlockSize();
        }

        ~OpenSSL11ProtectedMemoryAllocatorLP64()
        {
            ReleaseUnmanagedResources();
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
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

        public void SetReadAccess(IntPtr pointer, ulong length)
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

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
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
        public IntPtr Alloc(ulong length)
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
                systemInterface.SetNoDump(protectedMemory, length);
            }
            catch (Exception)
            {
                openSSL11.CRYPTO_secure_free(protectedMemory);
                throw;
            }

            return protectedMemory;
        }

        public void Free(IntPtr pointer, ulong length)
        {
            if (disposedValue)
            {
                throw new Exception("Called Free on disposed OpenSSL11ProtectedMemoryAllocatorLP64");
            }

            // Round up allocation size to nearest block size
            length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);

            Check.IntPtr(pointer, "OpenSSL11ProtectedMemoryAllocatorLP64.Free");

            Debug.WriteLine($"OpenSSL11ProtectedMemoryAllocatorLP64: Free({pointer},{length})");
            openSSL11.CRYPTO_secure_clear_free(pointer, length);
        }

        public void Dispose()
        {
            disposedValue = true;
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }
    }
}
