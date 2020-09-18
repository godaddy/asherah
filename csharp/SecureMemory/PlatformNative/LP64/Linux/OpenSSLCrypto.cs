using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Matching native conventions")]
    public class OpenSSLCrypto
    {
        public const string LibraryName = "libcrypto.so.1.1";
        public const int EVP_MAX_BLOCK_LENGTH = 32;
        public const int EVP_MAX_KEY_LENGTH = 64;
        public const int EVP_MAX_IV_LENGTH = 16;

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_new", SetLastError = true)]
        private static extern IntPtr _EVP_CIPHER_CTX_new();

        public IntPtr EVP_CIPHER_CTX_new()
        {
            return _EVP_CIPHER_CTX_new();
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_reset", SetLastError = true)]
        private static extern int _EVP_CIPHER_CTX_reset(IntPtr ctx);

        public int EVP_CIPHER_CTX_reset(IntPtr ctx)
        {
            return _EVP_CIPHER_CTX_reset(ctx);
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

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_block_size", SetLastError = true)]
        private static extern int _EVP_CIPHER_CTX_block_size(IntPtr e);

        public int EVP_CIPHER_CTX_block_size(IntPtr e)
        {
            return _EVP_CIPHER_CTX_block_size(e);
        }

        [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_block_size", SetLastError = true)]
        private static extern int _EVP_CIPHER_block_size(IntPtr e);

        public int EVP_CIPHER_block_size(IntPtr e)
        {
            return (int)_EVP_CIPHER_block_size(e);
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
    }
}
