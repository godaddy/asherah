using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux
{
    public class OpenSSLCryptProtectMemory : IDisposable
    {
        private readonly ulong pageSize = (ulong)Environment.SystemPageSize;
        private IntPtr encryptCtx = IntPtr.Zero;
        private IntPtr decryptCtx = IntPtr.Zero;
        private readonly IntPtr evpCipher = IntPtr.Zero;
        private IntPtr key = IntPtr.Zero;
        private readonly IntPtr iv = IntPtr.Zero;
        private readonly int blockSize;
        private bool disposedValue;
        private readonly object cryptProtectLock = new object();
        private readonly int protNone;
        private readonly int protRead;

        internal OpenSSLCryptProtectMemory(string cipher, LinuxProtectedMemoryAllocatorLP64 allocator)
        {
            protNone = allocator.GetProtNoAccess();
            protRead = allocator.GetProtRead();

            evpCipher = OpenSSLCrypto.EVP_get_cipherbyname(cipher);
            Check.ValidatePointer(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = OpenSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            var keySize = OpenSSLCrypto.EVP_CIPHER_key_length(evpCipher);
            Debug.WriteLine("Key length: " + keySize);

            var ivSize = OpenSSLCrypto.EVP_CIPHER_iv_length(evpCipher);
            Debug.WriteLine("IV length: " + ivSize);

            key = LibcLP64.mmap(IntPtr.Zero, pageSize, allocator.GetProtReadWrite(), allocator.GetPrivateAnonymousFlags(), -1, 0);
            Check.ValidatePointer(key, "mmap");

            var result = LibcLP64.mlock(key, pageSize);
            Check.Result(result, 0, "mlock");

            result = LibcLP64.madvise(key, pageSize, (int)Madvice.MADV_DONTDUMP);
            Check.Result(result, 0, "madvise");

            iv = IntPtr.Add(key, keySize);

            Debug.WriteLine("EVP_CIPHER_CTX_new encryptCtx");
            encryptCtx = OpenSSLCrypto.EVP_CIPHER_CTX_new();
            Check.ValidatePointer(encryptCtx, "EVP_CIPHER_CTX_new encryptCtx");

            Debug.WriteLine("EVP_CIPHER_CTX_new decryptCtx");
            decryptCtx = OpenSSLCrypto.EVP_CIPHER_CTX_new();
            Check.ValidatePointer(decryptCtx, "EVP_CIPHER_CTX_new decryptCtx");

            result = OpenSSLCrypto.RAND_bytes(key, keySize);
            Check.Result(result, 1, "RAND_bytes");

            result = OpenSSLCrypto.RAND_bytes(iv, ivSize);
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
            Debug.WriteLine($"CryptProtectMemory({memory}, {length}");

            if (disposedValue)
            {
                throw new SecureMemoryException("Called CryptProtectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + length + blockSize);
            var tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                LibcLP64.mlock(tmpBuffer, (ulong)length + (ulong)blockSize);
                LibcLP64.madvise(tmpBuffer, (ulong)length + (ulong)blockSize, (int)Madvice.MADV_DONTDUMP);

                lock (cryptProtectLock)
                {
                    int finalOutputLength;
                    LibcLP64.mprotect(key, pageSize, protRead);

                    try
                    {
                        Debug.WriteLine("EVP_EncryptInit_ex");
                        Check.ValidatePointer(encryptCtx, "CryptProtectMemory encryptCtx");
                        Check.ValidatePointer(key, "CryptProtectMemory key");
                        Check.ValidatePointer(iv, "CryptProtectMemory iv");
                        var result = OpenSSLCrypto.EVP_EncryptInit_ex(encryptCtx, evpCipher, IntPtr.Zero, key, iv);
                        Check.Result(result, 1, "EVP_EncryptInit_ex");

                        int outputLength;
                        Debug.WriteLine("EVP_EncryptUpdate");
                        result = OpenSSLCrypto.EVP_EncryptUpdate(encryptCtx, tmpBuffer, out outputLength, memory, length);
                        Check.Result(result, 1, "EVP_EncryptUpdate");

                        Debug.WriteLine($"EVP_EncryptUpdate outputLength = {outputLength}");

                        var finalOutput = IntPtr.Add(tmpBuffer, outputLength);

                        Debug.WriteLine("EVP_EncryptFinal_ex");
                        result = OpenSSLCrypto.EVP_EncryptFinal_ex(encryptCtx, finalOutput, out finalOutputLength);
                        Check.Result(result, 1, "EVP_EncryptFinal_ex");
                        finalOutputLength += outputLength;
                        Debug.WriteLine($"EVP_EncryptFinal_ex outputLength = {finalOutputLength}");
                    }
                    finally
                    {
                        LibcLP64.mprotect(key, pageSize, protNone);
                    }

                    LinuxLibcLP64.memcpy(memory, tmpBuffer, (ulong)finalOutputLength);
                }
            }
            finally
            {
                Debug.WriteLine("FreeHGlobal");
                Marshal.FreeHGlobal(tmpBuffer);
            }
        }

        public void CryptUnprotectMemory(IntPtr memory, int length)
        {
            Debug.WriteLine($"CryptUnprotectMemory({memory}, {length})");

            if (disposedValue)
            {
                throw new SecureMemoryException("Called CryptUnprotectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + length + blockSize);
            var tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                LibcLP64.mlock(tmpBuffer, (ulong)length + (ulong)blockSize);
                LibcLP64.madvise(tmpBuffer, (ulong)length + (ulong)blockSize, (int)Madvice.MADV_DONTDUMP);

                lock (cryptProtectLock)
                {
                    int finalDecryptedLength;
                    LibcLP64.mprotect(key, pageSize, protRead);
                    try
                    {
                        Debug.WriteLine("EVP_DecryptInit_ex");
                        Check.ValidatePointer(decryptCtx, "CryptUnprotectMemory decryptCtx is invalid");
                        Check.ValidatePointer(key, "CryptUnprotectMemory key is invalid");
                        Check.ValidatePointer(iv, "CryptUnprotectMemory iv is invalid");
                        var result = OpenSSLCrypto.EVP_DecryptInit_ex(decryptCtx, evpCipher, IntPtr.Zero, key, iv);
                        Check.Result(result, 1, "EVP_DecryptInit_ex");

                        int decryptedLength;
                        Debug.WriteLine("EVP_DecryptUpdate");
                        result = OpenSSLCrypto.EVP_DecryptUpdate(decryptCtx, tmpBuffer, out decryptedLength, memory, length);
                        Check.Result(result, 1, "EVP_DecryptUpdate");
                        Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                        var finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                        Debug.WriteLine("EVP_DecryptFinal_ex");
                        result = OpenSSLCrypto.EVP_DecryptFinal_ex(decryptCtx, finalDecrypted, out finalDecryptedLength);
                        finalDecryptedLength += decryptedLength;
                        Debug.WriteLine($"EVP_DecryptFinal_ex finalDecryptedLength = {finalDecryptedLength}");
                    }
                    finally
                    {
                        LibcLP64.mprotect(key, pageSize, protNone);
                    }

                    Debug.WriteLine("memcpy");
                    LinuxLibcLP64.memcpy(memory, tmpBuffer, (ulong)finalDecryptedLength);
                }
            }
            finally
            {
                Debug.WriteLine("FreeHGlobal");
                Marshal.FreeHGlobal(tmpBuffer);
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
                try
                {
                    if (disposing)
                    {
                        Monitor.Enter(cryptProtectLock);
                    }

                    Debug.WriteLine("EVP_CIPHER_CTX_free encryptCtx");
                    OpenSSLCrypto.EVP_CIPHER_CTX_free(encryptCtx);
                    encryptCtx = IntPtr.Zero;

                    Debug.WriteLine("EVP_CIPHER_CTX_free decryptCtx");
                    OpenSSLCrypto.EVP_CIPHER_CTX_free(decryptCtx);
                    decryptCtx = IntPtr.Zero;

                    Debug.WriteLine($"munmap({key}, {pageSize})");
                    LibcLP64.munmap(key, pageSize);
                    key = IntPtr.Zero;
                }
                finally
                {
                    if (disposing)
                    {
                        Monitor.Exit(cryptProtectLock);
                    }
                }

                disposedValue = true;
            }
        }
    }
}
