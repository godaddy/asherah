using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.OpenSSL;
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
        private IMemoryEncryption memoryEncryption;

        public OpenSSL11ProtectedMemoryAllocatorLP64(
            IConfiguration configuration,
            SystemInterface systemInterface,
            IMemoryEncryption memoryEncryption,
            IOpenSSLCrypto openSSL11)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var heapSizeConfig = configuration["heapSize"];
            var heapSize = !string.IsNullOrWhiteSpace(heapSizeConfig) ? ulong.Parse(heapSizeConfig) : DefaultHeapSize;

            var minimumAllocationSizeConfig = configuration["minimumAllocationSize"];
            var minimumAllocationSize = !string.IsNullOrWhiteSpace(minimumAllocationSizeConfig)
                ? int.Parse(minimumAllocationSizeConfig) : DefaultMinimumAllocationSize;

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

            int result = this.openSSL11.CRYPTO_secure_malloc_init(heapSize, minimumAllocationSize);
            switch (result)
            {
                case 0:
                    throw new OpenSSLSecureHeapUnavailableException("Unable to initialize OpenSSL secure heap");
                case 1:
                    break;
                case 2:
                    throw new OpenSSLSecureHeapUnavailableException("OpenSSL indicated insecure heap");
                default:
                    throw new OpenSSLSecureHeapUnavailableException("Unknown result from CRYPTO_secure_malloc_init");
            }

            this.systemInterface = systemInterface ?? throw new ArgumentNullException(nameof(systemInterface));
            this.memoryEncryption = memoryEncryption;
            encryptedMemoryBlockSize = this.memoryEncryption.GetEncryptedMemoryBlockSize();
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

            memoryEncryption.ProcessEncryptMemory(pointer, length);
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
            // Debug.WriteLine($"SetReadAccess: Rounding length {length} to nearest blocksize");
            // length = (length + (encryptedMemoryBlockSize - 1)) & ~(encryptedMemoryBlockSize - 1);
            // Debug.WriteLine($"SetReadAccess: New length {length}");
            memoryEncryption.ProcessDecryptMemory(pointer, length);
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

            memoryEncryption.ProcessDecryptMemory(pointer, length);
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
