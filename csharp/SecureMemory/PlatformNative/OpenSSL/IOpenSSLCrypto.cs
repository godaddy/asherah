using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.OpenSSL
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Matching native conventions")]
    public interface IOpenSSLCrypto
    {
        void LibraryCheck();

        void CheckResult(IntPtr ptr, string function);

        void CheckResult(int result, int expected, string function);

        ulong ERR_get_error();

        string ERR_error_string_n(ulong e);

        int CRYPTO_secure_malloc_init(size_t size, int minsize);

        int CRYPTO_secure_malloc_initialized();

        int CRYPTO_secure_malloc_done();

        IntPtr CRYPTO_secure_malloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0);

        IntPtr CRYPTO_secure_zalloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0);

        void CRYPTO_secure_free(IntPtr ptr, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0);

        void CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0);

        size_t CRYPTO_secure_used();

        IntPtr EVP_CIPHER_CTX_new();

        void EVP_CIPHER_CTX_free(IntPtr ctx);

        IntPtr EVP_get_cipherbyname(string name);

        int EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        int EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, out int outLength, IntPtr inPtr, int inLength);

        int EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength);

        int EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

        int EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, out int outlength, IntPtr inptr, int inlength);

        int EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength);

        int RAND_bytes(IntPtr buf, int num);

        int EVP_CIPHER_block_size(IntPtr e);

        int EVP_CIPHER_key_length(IntPtr e);

        int EVP_CIPHER_iv_length(IntPtr e);

        int EVP_CIPHER_CTX_reset(IntPtr ctx);
    }
}
