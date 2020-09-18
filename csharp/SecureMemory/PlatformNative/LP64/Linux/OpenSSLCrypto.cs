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
    public class OpenSSLCrypto
    {
        // (inl + cipher_block_size - 1)
        // EVP_EncryptUpdate() encrypts inl bytes from the buffer in and writes the encrypted version to out. This function can be called multiple times to encrypt successive blocks of data. The amount of data written depends on the block alignment of the encrypted data: as a result the amount of data written may be anything from zero bytes to (inl + cipher_block_size - 1) so out should contain sufficient room. The actual number of bytes written is placed in outl.
        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_CTX_new", SetLastError = true)]
        private static extern IntPtr _EVP_CIPHER_CTX_new();

        public IntPtr EVP_CIPHER_CTX_new()
        {
            return _EVP_CIPHER_CTX_new();
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_CTX_reset", SetLastError = true)]
        private static extern int _EVP_CIPHER_CTX_reset(IntPtr ctx);

        public int EVP_CIPHER_CTX_reset(IntPtr ctx)
        {
            return _EVP_CIPHER_CTX_reset(ctx);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_CTX_free", SetLastError = true)]
        private static extern void _EVP_CIPHER_CTX_free(IntPtr ctx);

        public void EVP_CIPHER_CTX_free(IntPtr ctx)
        {
            _EVP_CIPHER_CTX_free(ctx);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_get_cipherbyname", SetLastError = true)]
        private static extern IntPtr _EVP_get_cipherbyname([MarshalAs(UnmanagedType.LPStr)] string name);

        public IntPtr EVP_get_cipherbyname(string name)
        {
            return _EVP_get_cipherbyname(name);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_EncryptInit_ex", SetLastError = true)]
        private static extern int _EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        public int EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
        {
            return _EVP_EncryptInit_ex(ctx, type, impl, key, iv);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_EncryptUpdate", SetLastError = true)]
        private static extern int _EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outLength, IntPtr inptr, int inLength);

        public int EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, out int outLength, IntPtr inPtr, int inLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_EncryptUpdate(ctx, outPtr, outLengthBuf, inPtr, inLength);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_EncryptFinal_ex", SetLastError = true)]
        private static extern int _EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

        public int EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_EncryptFinal_ex(ctx, outPtr, outLengthBuf);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_DecryptInit_ex", SetLastError = true)]
        private static extern int _EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        public int EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
        {
            return _EVP_DecryptInit_ex(ctx, type, impl, key, iv);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_DecryptUpdate", SetLastError = true)]
        private static extern int _EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength, IntPtr inptr, int inlength);

        public int EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, out int outlength, IntPtr inptr, int inlength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_DecryptUpdate(ctx, outptr, outLengthBuf, inptr, inlength);

            outlength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_DecryptFinal_ex", SetLastError = true)]
        private static extern int _EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

        public int EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
        {
            byte[] outLengthBuf = new byte[4];

            int result = _EVP_DecryptFinal_ex(ctx, outPtr, outLengthBuf);

            outLength = BitConverter.ToInt32(outLengthBuf, 0);

            return result;
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "RAND_bytes", SetLastError = true)]
        private static extern int _RAND_bytes(IntPtr buf, int num);

        public int RAND_bytes(IntPtr buf, int num)
        {
            return _RAND_bytes(buf, num);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_CTX_block_size", SetLastError = true)]
        private static extern int _EVP_CIPHER_CTX_block_size(IntPtr e);

        public int EVP_CIPHER_CTX_block_size(IntPtr e)
        {
            return _EVP_CIPHER_CTX_block_size(e);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_block_size", SetLastError = true)]
        private static extern int _EVP_CIPHER_block_size(IntPtr e);

        public int EVP_CIPHER_block_size(IntPtr e)
        {
            return (int)_EVP_CIPHER_block_size(e);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_key_length", SetLastError = true)]
        private static extern int _EVP_CIPHER_key_length(IntPtr e);

        public int EVP_CIPHER_key_length(IntPtr e)
        {
            return _EVP_CIPHER_key_length(e);
        }

        [DllImport("libcrypto.so.1.1", EntryPoint = "EVP_CIPHER_iv_length", SetLastError = true)]
        private static extern int _EVP_CIPHER_iv_length(IntPtr e);

        public int EVP_CIPHER_iv_length(IntPtr e)
        {
            return _EVP_CIPHER_iv_length(e);
        }

        /*
         *              int EVP_CIPHER_block_size(const EVP_CIPHER *e);
             int EVP_CIPHER_key_length(const EVP_CIPHER *e);
             int EVP_CIPHER_iv_length(const EVP_CIPHER *e);
         */
        /*
             #include <openssl/evp.h>

             EVP_CIPHER_CTX *EVP_CIPHER_CTX_new(void);
             int EVP_CIPHER_CTX_reset(EVP_CIPHER_CTX *ctx);
             void EVP_CIPHER_CTX_free(EVP_CIPHER_CTX *ctx);

             int EVP_EncryptInit_ex(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                    ENGINE *impl, const unsigned char *key, const unsigned char *iv);
             int EVP_EncryptUpdate(EVP_CIPHER_CTX *ctx, unsigned char *out,
                                   int *outl, const unsigned char *in, int inl);
             int EVP_EncryptFinal_ex(EVP_CIPHER_CTX *ctx, unsigned char *out, int *outl);

             int EVP_DecryptInit_ex(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                    ENGINE *impl, const unsigned char *key, const unsigned char *iv);
             int EVP_DecryptUpdate(EVP_CIPHER_CTX *ctx, unsigned char *out,
                                   int *outl, const unsigned char *in, int inl);
             int EVP_DecryptFinal_ex(EVP_CIPHER_CTX *ctx, unsigned char *outm, int *outl);

             int EVP_CipherInit_ex(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                   ENGINE *impl, const unsigned char *key, const unsigned char *iv, int enc);
             int EVP_CipherUpdate(EVP_CIPHER_CTX *ctx, unsigned char *out,
                                  int *outl, const unsigned char *in, int inl);
             int EVP_CipherFinal_ex(EVP_CIPHER_CTX *ctx, unsigned char *outm, int *outl);

             int EVP_EncryptInit(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                 const unsigned char *key, const unsigned char *iv);
             int EVP_EncryptFinal(EVP_CIPHER_CTX *ctx, unsigned char *out, int *outl);

             int EVP_DecryptInit(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                 const unsigned char *key, const unsigned char *iv);
             int EVP_DecryptFinal(EVP_CIPHER_CTX *ctx, unsigned char *outm, int *outl);

             int EVP_CipherInit(EVP_CIPHER_CTX *ctx, const EVP_CIPHER *type,
                                const unsigned char *key, const unsigned char *iv, int enc);
             int EVP_CipherFinal(EVP_CIPHER_CTX *ctx, unsigned char *outm, int *outl);

             int EVP_CIPHER_CTX_set_padding(EVP_CIPHER_CTX *x, int padding);
             int EVP_CIPHER_CTX_set_key_length(EVP_CIPHER_CTX *x, int keylen);
             int EVP_CIPHER_CTX_ctrl(EVP_CIPHER_CTX *ctx, int type, int arg, void *ptr);
             int EVP_CIPHER_CTX_rand_key(EVP_CIPHER_CTX *ctx, unsigned char *key);

             const EVP_CIPHER *EVP_get_cipherbyname(const char *name);
             const EVP_CIPHER *EVP_get_cipherbynid(int nid);
             const EVP_CIPHER *EVP_get_cipherbyobj(const ASN1_OBJECT *a);

             int EVP_CIPHER_nid(const EVP_CIPHER *e);
             int EVP_CIPHER_block_size(const EVP_CIPHER *e);
             int EVP_CIPHER_key_length(const EVP_CIPHER *e);
             int EVP_CIPHER_iv_length(const EVP_CIPHER *e);
             unsigned long EVP_CIPHER_flags(const EVP_CIPHER *e);
             unsigned long EVP_CIPHER_mode(const EVP_CIPHER *e);
             int EVP_CIPHER_type(const EVP_CIPHER *ctx);

             const EVP_CIPHER *EVP_CIPHER_CTX_cipher(const EVP_CIPHER_CTX *ctx);
             int EVP_CIPHER_CTX_nid(const EVP_CIPHER_CTX *ctx);
             int EVP_CIPHER_CTX_block_size(const EVP_CIPHER_CTX *ctx);
             int EVP_CIPHER_CTX_key_length(const EVP_CIPHER_CTX *ctx);
             int EVP_CIPHER_CTX_iv_length(const EVP_CIPHER_CTX *ctx);
             void *EVP_CIPHER_CTX_get_app_data(const EVP_CIPHER_CTX *ctx);
             void EVP_CIPHER_CTX_set_app_data(const EVP_CIPHER_CTX *ctx, void *data);
             int EVP_CIPHER_CTX_type(const EVP_CIPHER_CTX *ctx);
             int EVP_CIPHER_CTX_mode(const EVP_CIPHER_CTX *ctx);

             int EVP_CIPHER_param_to_asn1(EVP_CIPHER_CTX *c, ASN1_TYPE *type);
             int EVP_CIPHER_asn1_to_param(EVP_CIPHER_CTX *c, ASN1_TYPE *type);
         */
    }
}
