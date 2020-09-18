using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    public class OpenSSLCryptProtectMemory : IDisposable
    {
        private const int PageSize = 4096;
        private OpenSSLCrypto openSSLCrypto;
        private IntPtr encryptCtx = IntPtr.Zero;
        private IntPtr decryptCtx = IntPtr.Zero;
        private IntPtr evpCipher = IntPtr.Zero;
        private IntPtr key = IntPtr.Zero;
        private IntPtr iv = IntPtr.Zero;
        private int blockSize;
        private bool disposedValue;
        private LinuxOpenSSL11LP64 openSSL11;
        private object cryptProtectLock = new object();
        private int protNone;
        private int protRead;
        private int protReadWrite;

        internal OpenSSLCryptProtectMemory(string cipher, LinuxProtectedMemoryAllocatorLP64 allocator)
        {
            openSSL11 = new LinuxOpenSSL11LP64();
            openSSLCrypto = new OpenSSLCrypto();

            protNone = allocator.GetProtNoAccess();
            protRead = allocator.GetProtRead();
            protReadWrite = allocator.GetProtReadWrite();

            evpCipher = openSSLCrypto.EVP_get_cipherbyname(cipher);
            Check.IntPtr(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = openSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            int keySize = openSSLCrypto.EVP_CIPHER_key_length(evpCipher);
            Debug.WriteLine("Key length: " + keySize);

            int ivSize = openSSLCrypto.EVP_CIPHER_iv_length(evpCipher);
            Debug.WriteLine("IV length: " + ivSize);

            key = openSSL11.mmap(IntPtr.Zero, PageSize, allocator.GetProtReadWrite(), allocator.GetPrivateAnonymousFlags(), -1, 0);
            Check.IntPtr(key, "mmap");

            int result = openSSL11.mlock(key, PageSize);
            Check.Result(result, 0, "mlock");

            result = openSSL11.madvise(key, PageSize, (int)Madvice.MADV_DONTDUMP);
            Check.Result(result, 0, "madvise");

            iv = IntPtr.Add(key, keySize);

            Debug.WriteLine("EVP_CIPHER_CTX_new encryptCtx");
            encryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            Check.IntPtr(encryptCtx, "EVP_CIPHER_CTX_new encryptCtx");

            Debug.WriteLine("EVP_CIPHER_CTX_new decryptCtx");
            decryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            Check.IntPtr(decryptCtx, "EVP_CIPHER_CTX_new decryptCtx");

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
            lock (cryptProtectLock)
            {
                Debug.WriteLine("AllocHGlobal for tmpBuffer");
                IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
                try
                {
                    openSSL11.mlock(tmpBuffer, (ulong)length + (ulong)blockSize);

                    openSSL11.mprotect(key, PageSize, protRead);

                    Debug.WriteLine("EVP_EncryptInit_ex");
                    Check.IntPtr(encryptCtx, "CryptProtectMemory encryptCtx");
                    Check.IntPtr(key, "CryptProtectMemory key");
                    Check.IntPtr(iv, "CryptProtectMemory iv");
                    int result = openSSLCrypto.EVP_EncryptInit_ex(encryptCtx, evpCipher, IntPtr.Zero, key, iv);
                    Check.Result(result, 1, "EVP_EncryptInit_ex");

                    int outputLength;
                    Debug.WriteLine("EVP_EncryptUpdate");
                    result = openSSLCrypto.EVP_EncryptUpdate(encryptCtx, tmpBuffer, out outputLength, memory, length);
                    Check.Result(result, 1, "EVP_EncryptUpdate");

                    Debug.WriteLine($"EVP_EncryptUpdate outputLength = {outputLength}");

                    IntPtr finalOutput = IntPtr.Add(tmpBuffer, outputLength);
                    int finalOutputLength;

                    Debug.WriteLine("EVP_EncryptFinal_ex");
                    result = openSSLCrypto.EVP_EncryptFinal_ex(encryptCtx, finalOutput, out finalOutputLength);
                    Check.Result(result, 1, "EVP_EncryptFinal_ex");
                    finalOutputLength += outputLength;
                    Debug.WriteLine($"EVP_EncryptFinal_ex outputLength = {finalOutputLength}");

                    openSSL11.mprotect(key, PageSize, protNone);

                    openSSL11.memcpy(memory, tmpBuffer, (ulong)finalOutputLength);
                }
                finally
                {
                    Debug.WriteLine("FreeHGlobal");
                    Marshal.FreeHGlobal(tmpBuffer);
                }
            }
        }

        public void CryptUnprotectMemory(IntPtr memory, int length)
        {
            lock (cryptProtectLock)
            {
                Debug.WriteLine("AllocHGlobal for tmpBuffer");
                IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
                try
                {
                    openSSL11.mlock(tmpBuffer, (ulong)length + (ulong)blockSize);

                    openSSL11.mprotect(key, PageSize, protRead);

                    Debug.WriteLine("EVP_DecryptInit_ex");
                    Check.IntPtr(decryptCtx, "CryptUnprotectMemory decryptCtx");
                    Check.IntPtr(key, "CryptUnprotectMemory key");
                    Check.IntPtr(iv, "CryptUnprotectMemory iv");
                    int result = openSSLCrypto.EVP_DecryptInit_ex(decryptCtx, evpCipher, IntPtr.Zero, key, iv);
                    Check.Result(result, 1, "EVP_DecryptInit_ex");

                    int decryptedLength;
                    Debug.WriteLine("EVP_DecryptUpdate");
                    result = openSSLCrypto.EVP_DecryptUpdate(decryptCtx, tmpBuffer, out decryptedLength, memory, length);
                    Check.Result(result, 1, "EVP_DecryptUpdate");
                    Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                    int finalDecryptedLength;
                    IntPtr finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                    Debug.WriteLine("EVP_DecryptFinal_ex");
                    result = openSSLCrypto.EVP_DecryptFinal_ex(decryptCtx, finalDecrypted, out finalDecryptedLength);
                    finalDecryptedLength += decryptedLength;
                    Debug.WriteLine($"EVP_DecryptFinal_ex finalDecryptedLength = {finalDecryptedLength}");

                    openSSL11.mprotect(key, PageSize, protNone);

                    Debug.WriteLine("memcpy");
                    openSSL11.memcpy(memory, tmpBuffer, (ulong)finalDecryptedLength);
                }
                finally
                {
                    Debug.WriteLine("FreeHGlobal");
                    Marshal.FreeHGlobal(tmpBuffer);
                }
            }
        }

        public int GetBlockSize()
        {
            return blockSize;
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

                Debug.WriteLine("EVP_CIPHER_CTX_free encryptCtx");
                openSSLCrypto.EVP_CIPHER_CTX_free(encryptCtx);
                encryptCtx = IntPtr.Zero;

                Debug.WriteLine("EVP_CIPHER_CTX_free decryptCtx");
                openSSLCrypto.EVP_CIPHER_CTX_free(decryptCtx);
                decryptCtx = IntPtr.Zero;

                Debug.WriteLine("munmap");
                openSSL11ref.munmap(key, PageSize);
                key = IntPtr.Zero;

                disposedValue = true;
            }
        }
    }
}
