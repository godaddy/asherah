using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.OpenSSL11
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    public class LinuxOpenSSL11LP64 : LibcLP64
    {
        public bool IsAvailable()
        {
            try
            {
                CRYPTO_secure_malloc_initialized();
            }
            catch (DllNotFoundException)
            {
                return false;
            }

            return true;
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_malloc_init", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_init(size_t size, int minsize);

        public int CRYPTO_secure_malloc_init(size_t size, int minsize)
        {
            if ((size & (size - 1)) != 0)
            {
                throw new Exception("size must be power of 2");
            }

            if ((minsize & (minsize - 1)) != 0)
            {
                throw new Exception("minsize must be power of 2");
            }

            // CRYPTO_secure_malloc_init() returns 0 on failure, 1 if successful, and 2 if successful but the heap could not be protected by memory mapping.
            return _CRYPTO_secure_malloc_init(size, minsize);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_malloc_initialized", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_initialized();

        public int CRYPTO_secure_malloc_initialized()
        {
            // CRYPTO_secure_malloc_initialized() returns 1 if the secure heap is available (that is, if CRYPTO_secure_malloc_init() has been called, but CRYPTO_secure_malloc_done() has not been called or failed) or 0 if not.
            return _CRYPTO_secure_malloc_initialized();
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_malloc_done", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_done();

        public int CRYPTO_secure_malloc_done()
        {
            // CRYPTO_secure_malloc_done() returns 1 if the secure memory area is released, or 0 if not.
            return _CRYPTO_secure_malloc_done();
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_malloc", SetLastError = true)]
        private static extern IntPtr _CRYPTO_secure_malloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public IntPtr CRYPTO_secure_malloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure heap of the requested size, or NULL if memory could not be allocated.
            return _CRYPTO_secure_malloc(num, file, line);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_zalloc", SetLastError = true)]
        private static extern IntPtr _CRYPTO_secure_zalloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public IntPtr CRYPTO_secure_zalloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure heap of the requested size, or NULL if memory could not be allocated.
            return _CRYPTO_secure_zalloc(num, file, line);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_free", SetLastError = true)]
        private static extern void _CRYPTO_secure_free(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public void CRYPTO_secure_free(IntPtr ptr, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_free() releases the memory at ptr back to the heap.
            _CRYPTO_secure_free(ptr, file, line);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_clear_free", SetLastError = true)]
        private static extern void _CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public void CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_free() releases the memory at ptr back to the heap.
            _CRYPTO_secure_clear_free(ptr, num, file, line);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "CRYPTO_secure_used", SetLastError = true)]
        private static extern size_t _CRYPTO_secure_used();

        public size_t CRYPTO_secure_used()
        {
            // CRYPTO_secure_used() returns the number of bytes allocated in the secure heap.
            return _CRYPTO_secure_used();
        }
    }
}
