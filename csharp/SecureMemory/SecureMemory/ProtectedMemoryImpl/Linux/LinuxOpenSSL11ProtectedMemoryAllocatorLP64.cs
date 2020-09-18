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

        private static LinuxOpenSSL11LP64 openSSL11;
        private static int refCount = 0;
        private static object openSSL11Lock = new object();

        private OpenSSLCryptProtectMemory cryptProtectMemory;

        public LinuxOpenSSL11ProtectedMemoryAllocatorLP64(ulong size, int minsize)
            : base((LinuxLibcLP64)new LinuxOpenSSL11LP64())
        {
            lock (openSSL11Lock)
            {
                if (openSSL11 == null)
                {
                    openSSL11 = (LinuxOpenSSL11LP64)GetLibc();
                    if (openSSL11 == null)
                    {
                        throw new Exception("GetLibc returned null object for openSSL11");
                    }

                    Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorLP64: openSSL11 is not null");
                }

                if (refCount == 0)
                {
                    Debug.WriteLine($"*** LinuxOpenSSL11ProtectedMemoryAllocatorLP64: CRYPTO_secure_malloc_init ***");
                    try
                    {
                        Check.Result(openSSL11.CRYPTO_secure_malloc_init(size, minsize), 1, "CRYPTO_secure_malloc_init");
                    }
                    catch (Exception)
                    {
                        // Check.Result(openSSL11.CRYPTO_secure_malloc_done(), 1, "CRYPTO_secure_malloc_done");
                        throw;
                    }
                }
                else
                {
                    Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorLP64: refCount is > 0, not calling CRYPTO_secure_malloc_init");
                }

                refCount++;
                Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64: ctor New refCount is {refCount}");

                cryptProtectMemory = new OpenSSLCryptProtectMemory("aes-256-gcm", this);
            }
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
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            Debug.WriteLine("CryptProtectMemory");
            cryptProtectMemory.CryptProtectMemory(pointer, (int)length);
        }

        public override void SetReadAccess(IntPtr pointer, ulong length)
        {
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            Debug.WriteLine("CryptUnprotectMemory");
            cryptProtectMemory.CryptUnprotectMemory(pointer, (int)length);
        }

        public override void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            // Per page-protections aren't possible with the OpenSSL secure heap implementation
            Debug.WriteLine("CryptUnprotectMemory");
            cryptProtectMemory.CryptUnprotectMemory(pointer, (int)length);
        }

        // ************************************
        // alloc / free
        // ************************************
        public override IntPtr Alloc(ulong length)
        {
            // TODO: Round up allocation size to nearest AES-256 block size
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
            // TODO: Round up allocation size to nearest AES-256 block size
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
            if (!disposing)
            {
                throw new Exception("FATAL: Reached finalizer for LinuxOpenSSL11ProtectedMemoryAllocator (missing Dispose())");
            }

            lock (openSSL11Lock)
            {
                if (openSSL11 == null)
                {
                    throw new Exception("LinuxOpenSSL11ProtectedMemoryAllocatorLP64.Dispose: openSSL11 is null!");
                }

                Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64 refCount is {refCount}");
                refCount--;
                Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64 new refCount is {refCount}");
                if (refCount == 0)
                {
                    Debug.WriteLine($"*** LinuxOpenSSL11ProtectedMemoryAllocatorLP64: CRYPTO_secure_malloc_done ***");
                    int result = openSSL11.CRYPTO_secure_malloc_done();
                    Debug.WriteLine("Best effort call to CRYPTO_secure_malloc_done returned " + result);
                }
                else
                {
                    Debug.WriteLine($"LinuxOpenSSL11ProtectedMemoryAllocatorLP64 Skipping CRYPTO_secure_malloc_done due to refCount {refCount}");
                }
            }
        }

        protected override void ZeroMemory(IntPtr pointer, ulong length)
        {
            // CRYPTO_secure_clear_free includes ZeroMemory functionality
        }
    }
}
