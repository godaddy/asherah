using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    public class OpenSSLCryptProtectMemory : IDisposable
    {
        private const int PageSize = 4096;
        private OpenSSLCrypto openSSLCrypto;
        private IntPtr ctx = IntPtr.Zero;
        private IntPtr evpCipher = IntPtr.Zero;
        private IntPtr key = IntPtr.Zero;
        private IntPtr iv = IntPtr.Zero;
        private int blockSize;
        private bool disposedValue;
        private LinuxOpenSSL11LP64 openSSL11;

        internal OpenSSLCryptProtectMemory(string cipher, LinuxProtectedMemoryAllocatorLP64 allocator)
        {
            openSSL11 = new LinuxOpenSSL11LP64();
            openSSLCrypto = new OpenSSLCrypto();

            evpCipher = openSSLCrypto.EVP_get_cipherbyname(cipher);
            Check.IntPtr(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = openSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            // For some reason this returns 1 instead of 16 in my testing???
            if (blockSize < 16)
            {
                blockSize = 16;
                Debug.WriteLine("BUG: Adjusted block size: " + blockSize);
            }

            int keySize = openSSLCrypto.EVP_CIPHER_key_length(evpCipher);
            Debug.WriteLine("Key length: " + keySize);

            int ivSize = openSSLCrypto.EVP_CIPHER_iv_length(evpCipher);
            Debug.WriteLine("IV length: " + ivSize);

            key = openSSL11.mmap(IntPtr.Zero, PageSize, allocator.GetProtReadWrite(), allocator.GetPrivateAnonymousFlags(), -1, 0);
            Check.IntPtr(key, "mmap");

            int result = openSSL11.mlock(key, PageSize);
            Check.Result(result, 0, "mlock");

            iv = IntPtr.Add(key, keySize);

            Debug.WriteLine("EVP_CIPHER_CTX_new");
            IntPtr ctx = openSSLCrypto.EVP_CIPHER_CTX_new();
            Check.IntPtr(ctx, "EVP_CIPHER_CTX_new");

            result = openSSLCrypto.RAND_bytes(key, keySize);
            Check.Result(result, 1, "RAND_bytes");

            result = openSSLCrypto.RAND_bytes(iv, ivSize);
            Check.Result(result, 1, "RAND_bytes");
        }

        ~OpenSSLCryptProtectMemory()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void CryptProtectMemory(IntPtr memory, int length)
        {
            IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                int result = openSSLCrypto.EVP_EncryptInit_ex(ctx, evpCipher, IntPtr.Zero, key, iv);
                Check.Result(result, 1, "EVP_EncryptInit_ex");

                int outputLength;
                result = openSSLCrypto.EVP_EncryptUpdate(ctx, tmpBuffer, out outputLength, memory, length);
                Check.Result(result, 1, "EVP_EncryptUpdate");

                Debug.WriteLine($"EVP_EncryptUpdate outputLength = {outputLength}");

                IntPtr finalOutput = IntPtr.Add(tmpBuffer, outputLength);
                int finalOutputLength;

                result = openSSLCrypto.EVP_EncryptFinal_ex(ctx, finalOutput, out finalOutputLength);
                Check.Result(result, 1, "EVP_EncryptFinal_ex");
                finalOutputLength += outputLength;
                Debug.WriteLine($"EVP_EncryptFinal_ex outputLength = {finalOutputLength}");

                openSSL11.memcpy(memory, tmpBuffer, (ulong)finalOutputLength);
            }
            finally
            {
                Marshal.FreeHGlobal(tmpBuffer);
            }
        }

        public void CryptUnprotectMemory(IntPtr memory, int length)
        {
            IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                int result = openSSLCrypto.EVP_DecryptInit_ex(ctx, evpCipher, IntPtr.Zero, key, iv);
                Check.Result(result, 1, "EVP_DecryptInit_ex");

                int decryptedLength;
                result = openSSLCrypto.EVP_DecryptUpdate(ctx, tmpBuffer, out decryptedLength, memory, length);
                Check.Result(result, 1, "EVP_DecryptUpdate");
                Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                int finalDecryptedLength;
                IntPtr finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                result = openSSLCrypto.EVP_DecryptFinal_ex(ctx, finalDecrypted, out finalDecryptedLength);
                finalDecryptedLength += decryptedLength;
                Debug.WriteLine($"EVP_DecryptFinal_ex finalDecryptedLength = {finalDecryptedLength}");

                openSSL11.memcpy(memory, tmpBuffer, (ulong)finalDecryptedLength);
            }
            finally
            {
                Marshal.FreeHGlobal(tmpBuffer);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                LinuxOpenSSL11LP64 openSSL11ref = null;
                if (disposing)
                {
                    openSSL11ref = openSSL11;
                }
                else
                {
                    openSSL11ref = new LinuxOpenSSL11LP64();
                }

                Debug.WriteLine("EVP_CIPHER_CTX_free");
                openSSLCrypto.EVP_CIPHER_CTX_free(ctx);

                Debug.WriteLine("munmap");
                openSSL11ref.munmap(key, PageSize);

                disposedValue = true;
            }
        }
    }
}
