using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using Microsoft.Extensions.Configuration;
using size_t = System.UInt64;

// ReSharper disable InconsistentNaming

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
namespace GoDaddy.Asherah.PlatformNative.OpenSSL
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Matching native conventions")]
    public class OpenSSLCryptoLibc : IOpenSSLCrypto
    {
        private const string LibraryName = "libcrypto.so.1.1";
        private const int EVP_MAX_BLOCK_LENGTH = 32;

        // ReSharper disable UnusedMember.Local
        private const int EVP_MAX_KEY_LENGTH = 64;
        private const int EVP_MAX_IV_LENGTH = 16;

        // ReSharper restore UnusedMember.Local
        public OpenSSLCryptoLibc(IConfiguration configuration)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var openSSLPath = configuration["openSSLPath"];
                if (!string.IsNullOrWhiteSpace(openSSLPath))
                {
                    _ = WindowsInterop.LoadLibrary(Path.Combine(openSSLPath, LibraryName));
                }
            }

            // ReSharper disable once VirtualMemberCallInConstructor
            LibraryCheck();
        }

        // This is virtual for testing library-not-found
        // ReSharper disable once MemberCanBeProtected.Global
        public virtual void LibraryCheck()
        {
            CRYPTO_secure_malloc_initialized();
        }

        public void CheckResult(int result, int expected, string function)
        {
            if (result != expected)
            {
                ulong err = ERR_get_error();
                throw new Exception($"{function}: {ERR_error_string_n(err)}");
            }
        }

        public void CheckResult(IntPtr result, string function)
        {
            if (result == IntPtr.Zero)
            {
                ulong err = ERR_get_error();
                throw new Exception($"{function}: {ERR_error_string_n(err)}");
            }
        }

        [DllImport(LibraryName, EntryPoint = "ERR_get_error", SetLastError = true)]
        private static extern UIntPtr _ERR_get_error();

        public ulong ERR_get_error()
        {
            return (ulong)_ERR_get_error();
        }

        [DllImport(LibraryName, EntryPoint = "ERR_error_string_n", SetLastError = true)]
        private static extern void _ERR_error_string_n(ulong e, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, size_t len);

        public string ERR_error_string_n(ulong e)
        {
            var buffer = new byte[256];
            _ERR_error_string_n(e, buffer, (ulong)buffer.LongLength);

            int end = Array.FindIndex(buffer, 0, x => x == 0);
            return Encoding.UTF8.GetString(buffer, 0, end);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_init", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_init(size_t size, int minsize);

        public int CRYPTO_secure_malloc_init(size_t size, int minsize)
        {
            // Round values up to nearest power of 2 as required by CRYPTO_secure_malloc_init
            size = (size_t)Math.Pow(2, (size_t)Math.Log(size - 1, 2) + 1);
            minsize = (int)Math.Pow(2, (int)Math.Log(minsize - 1, 2) + 1);

            if (CRYPTO_secure_malloc_initialized() == 1)
            {
                return 1;
            }

            // CRYPTO_secure_malloc_init() returns 0 on failure, 1 if successful, and 2 if
            // successful but the heap could not be protected by memory mapping.
            return _CRYPTO_secure_malloc_init(size, minsize);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_initialized", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_initialized();

        public int CRYPTO_secure_malloc_initialized()
        {
            // CRYPTO_secure_malloc_initialized() returns 1 if the secure heap is available
            // (that is, if CRYPTO_secure_malloc_init() has been called, but CRYPTO_secure_malloc_done()
            // has not been called or failed) or 0 if not.
            return _CRYPTO_secure_malloc_initialized();
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_done", SetLastError = true)]
        private static extern int _CRYPTO_secure_malloc_done();

        [ExcludeFromCodeCoverage]
        public int CRYPTO_secure_malloc_done()
        {
            // CRYPTO_secure_malloc_done() returns 1 if the secure memory area is released, or 0 if not.
            return _CRYPTO_secure_malloc_done();
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc", SetLastError = true)]
        private static extern IntPtr _CRYPTO_secure_malloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public IntPtr CRYPTO_secure_malloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure
            // heap of the requested size, or NULL if memory could not be allocated.
            return _CRYPTO_secure_malloc(num, file, line);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_zalloc", SetLastError = true)]
        private static extern IntPtr _CRYPTO_secure_zalloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        [ExcludeFromCodeCoverage]
        public IntPtr CRYPTO_secure_zalloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure heap
            // of the requested size, or NULL if memory could not be allocated.
            return _CRYPTO_secure_zalloc(num, file, line);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_free", SetLastError = true)]
        private static extern void _CRYPTO_secure_free(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public void CRYPTO_secure_free(IntPtr ptr, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_free() releases the memory at ptr back to the heap.
            _CRYPTO_secure_free(ptr, file, line);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_clear_free", SetLastError = true)]
        private static extern void _CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

        public void CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // OPENSSL_secure_free() releases the memory at ptr back to the heap.
            _CRYPTO_secure_clear_free(ptr, num, file, line);
        }

        [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_used", SetLastError = true)]
        private static extern size_t _CRYPTO_secure_used();

        [ExcludeFromCodeCoverage]
        public size_t CRYPTO_secure_used()
        {
            // CRYPTO_secure_used() returns the number of bytes allocated in the secure heap.
            return _CRYPTO_secure_used();
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_new", SetLastError = true)]
        private static extern IntPtr _EVP_CIPHER_CTX_new();

        public IntPtr EVP_CIPHER_CTX_new()
        {
            return _EVP_CIPHER_CTX_new();
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_free", SetLastError = true)]
        private static extern void _EVP_CIPHER_CTX_free(IntPtr ctx);

        public void EVP_CIPHER_CTX_free(IntPtr ctx)
        {
            _EVP_CIPHER_CTX_free(ctx);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_get_cipherbyname", SetLastError = true)]
        private static extern IntPtr _EVP_get_cipherbyname([MarshalAs(UnmanagedType.LPStr)] string name);

        public IntPtr EVP_get_cipherbyname(string name)
        {
            return _EVP_get_cipherbyname(name);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_EncryptInit_ex", SetLastError = true)]
        private static extern int _EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        public int EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
        {
            return _EVP_EncryptInit_ex(ctx, type, impl, key, iv);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_EncryptUpdate", SetLastError = true)]
        private static extern int _EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outLength, IntPtr inptr, int inLength);

        public int EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, out int outLength, IntPtr inPtr, int inLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_EncryptUpdate(ctx, outPtr, outLengthBuf, inPtr, inLength);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport(LibraryName, EntryPoint = "EVP_EncryptFinal_ex", SetLastError = true)]
        private static extern int _EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

        public int EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_EncryptFinal_ex(ctx, outPtr, outLengthBuf);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport(LibraryName, EntryPoint = "EVP_DecryptInit_ex", SetLastError = true)]
        private static extern int _EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        public int EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
        {
            return _EVP_DecryptInit_ex(ctx, type, impl, key, iv);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_DecryptUpdate", SetLastError = true)]
        private static extern int _EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength, IntPtr inptr, int inlength);

        public int EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, out int outlength, IntPtr inptr, int inlength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_DecryptUpdate(ctx, outptr, outLengthBuf, inptr, inlength);

            outlength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport(LibraryName, EntryPoint = "EVP_DecryptFinal_ex", SetLastError = true)]
        private static extern int _EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

        public int EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_DecryptFinal_ex(ctx, outPtr, outLengthBuf);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport(LibraryName, EntryPoint = "RAND_bytes", SetLastError = true)]
        private static extern int _RAND_bytes(IntPtr buf, int num);

        public int RAND_bytes(IntPtr buf, int num)
        {
            return _RAND_bytes(buf, num);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_block_size", SetLastError = true)]
        private static extern int _EVP_CIPHER_block_size(IntPtr e);

        public int EVP_CIPHER_block_size(IntPtr e)
        {
            int blockSize = _EVP_CIPHER_block_size(e);

            // BUG: EVP_CIPHER_block_size returns 1
            if (blockSize == 1)
            {
                blockSize = OpenSSLCryptoLibc.EVP_MAX_BLOCK_LENGTH;
                Debug.WriteLine("BUG: Adjusted block size: " + blockSize);
            }

            return blockSize;
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_key_length", SetLastError = true)]
        private static extern int _EVP_CIPHER_key_length(IntPtr e);

        public int EVP_CIPHER_key_length(IntPtr e)
        {
            return _EVP_CIPHER_key_length(e);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_iv_length", SetLastError = true)]
        private static extern int _EVP_CIPHER_iv_length(IntPtr e);

        public int EVP_CIPHER_iv_length(IntPtr e)
        {
            return _EVP_CIPHER_iv_length(e);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_reset", SetLastError = true)]
        private static extern int _EVP_CIPHER_CTX_reset(IntPtr ctx);

        public int EVP_CIPHER_CTX_reset(IntPtr ctx)
        {
            return _EVP_CIPHER_CTX_reset(ctx);
        }
    }
}
