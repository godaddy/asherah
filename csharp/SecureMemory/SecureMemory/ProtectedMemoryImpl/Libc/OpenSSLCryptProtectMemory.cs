using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    public class OpenSSLCryptProtectMemory : IDisposable
    {
        private readonly OpenSSLCrypto openSSLCrypto;
        private readonly object cryptProtectLock = new object();
        private readonly IntPtr iv;
        private readonly int blockSize;
        private readonly IntPtr evpCipher;
        private readonly SystemInterface systemInterface;
        private IntPtr encryptCtx;
        private IntPtr decryptCtx;
        private IntPtr key;
        private bool disposedValue;

        internal OpenSSLCryptProtectMemory(string cipher, SystemInterface systemInterface)
        {
            openSSLCrypto = new OpenSSLCrypto();
            this.systemInterface = systemInterface;

            evpCipher = openSSLCrypto.EVP_get_cipherbyname(cipher);
            Check.IntPtr(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = openSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            int keySize = openSSLCrypto.EVP_CIPHER_key_length(evpCipher);
            Debug.WriteLine("Key length: " + keySize);

            int ivSize = openSSLCrypto.EVP_CIPHER_iv_length(evpCipher);
            Debug.WriteLine("IV length: " + ivSize);

            key = systemInterface.PageAlloc((ulong)systemInterface.PageSize);
            Check.IntPtr(key, "mmap");

            systemInterface.LockMemory(key, (ulong)systemInterface.PageSize);
            systemInterface.SetNoDump(key, (ulong)systemInterface.PageSize);

            iv = IntPtr.Add(key, keySize);

            Debug.WriteLine("EVP_CIPHER_CTX_new encryptCtx");
            encryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            Check.IntPtr(encryptCtx, "EVP_CIPHER_CTX_new encryptCtx");

            Debug.WriteLine("EVP_CIPHER_CTX_new decryptCtx");
            decryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            Check.IntPtr(decryptCtx, "EVP_CIPHER_CTX_new decryptCtx");

            var result = openSSLCrypto.RAND_bytes(key, keySize);
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
            Debug.WriteLine($"CryptProtectMemory({memory}, {length}");

            if (disposedValue)
            {
                throw new Exception("Called CryptProtectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + length + blockSize);
            IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                systemInterface.LockMemory(tmpBuffer, (ulong)length + (ulong)blockSize);
                systemInterface.SetNoDump(tmpBuffer, (ulong)length + (ulong)blockSize);

                lock (cryptProtectLock)
                {
                    int finalOutputLength;
                    systemInterface.SetReadAccess(key, (ulong)systemInterface.PageSize);

                    try
                    {
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

                        Debug.WriteLine("EVP_EncryptFinal_ex");
                        result = openSSLCrypto.EVP_EncryptFinal_ex(encryptCtx, finalOutput, out finalOutputLength);
                        Check.Result(result, 1, "EVP_EncryptFinal_ex");
                        finalOutputLength += outputLength;
                        Debug.WriteLine($"EVP_EncryptFinal_ex outputLength = {finalOutputLength}");
                    }
                    finally
                    {
                        systemInterface.SetNoAccess(key, (ulong)systemInterface.PageSize);
                    }

                    systemInterface.CopyMemory(tmpBuffer, memory, (ulong)finalOutputLength);
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
                throw new Exception("Called CryptUnprotectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + length + blockSize);
            IntPtr tmpBuffer = Marshal.AllocHGlobal(length + blockSize);
            try
            {
                systemInterface.LockMemory(tmpBuffer, (ulong)length + (ulong)blockSize);
                systemInterface.SetNoDump(tmpBuffer, (ulong)length + (ulong)blockSize);

                lock (cryptProtectLock)
                {
                    int finalDecryptedLength;
                    systemInterface.SetReadAccess(key, (ulong)systemInterface.PageSize);
                    try
                    {
                        Debug.WriteLine("EVP_DecryptInit_ex");
                        Check.IntPtr(decryptCtx, "CryptUnprotectMemory decryptCtx is invalid");
                        Check.IntPtr(key, "CryptUnprotectMemory key is invalid");
                        Check.IntPtr(iv, "CryptUnprotectMemory iv is invalid");
                        int result = openSSLCrypto.EVP_DecryptInit_ex(decryptCtx, evpCipher, IntPtr.Zero, key, iv);
                        Check.Result(result, 1, "EVP_DecryptInit_ex");

                        int decryptedLength;
                        Debug.WriteLine("EVP_DecryptUpdate");
                        result = openSSLCrypto.EVP_DecryptUpdate(decryptCtx, tmpBuffer, out decryptedLength, memory, length);
                        Check.Result(result, 1, "EVP_DecryptUpdate");
                        Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                        IntPtr finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                        Debug.WriteLine("EVP_DecryptFinal_ex");
                        result = openSSLCrypto.EVP_DecryptFinal_ex(decryptCtx, finalDecrypted, out finalDecryptedLength);
                        Check.Result(result, 1, "EVP_DecryptFinal_ex");
                        finalDecryptedLength += decryptedLength;
                        Debug.WriteLine($"EVP_DecryptFinal_ex finalDecryptedLength = {finalDecryptedLength}");
                    }
                    finally
                    {
                        systemInterface.SetNoAccess(key, (ulong)systemInterface.PageSize);
                    }

                    Debug.WriteLine("memcpy");
                    systemInterface.CopyMemory(tmpBuffer, memory, (ulong)finalDecryptedLength);
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
                SystemInterface disposeSystemInterface;
                try
                {
                    if (disposing)
                    {
                        disposeSystemInterface = systemInterface;
                        Monitor.Enter(cryptProtectLock);
                    }
                    else
                    {
                        disposeSystemInterface = SystemInterface.GetInstance();
                    }

                    Debug.WriteLine("EVP_CIPHER_CTX_free encryptCtx");
                    openSSLCrypto.EVP_CIPHER_CTX_free(encryptCtx);
                    encryptCtx = IntPtr.Zero;

                    Debug.WriteLine("EVP_CIPHER_CTX_free decryptCtx");
                    openSSLCrypto.EVP_CIPHER_CTX_free(decryptCtx);
                    decryptCtx = IntPtr.Zero;

                    Debug.WriteLine($"PageFree({key}, {disposeSystemInterface.PageSize})");
                    disposeSystemInterface.PageFree(key, (ulong)disposeSystemInterface.PageSize);
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
