using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GoDaddy.Asherah.PlatformNative.OpenSSL
{
    public class OpenSSLCryptProtectMemory : IMemoryEncryption, IDisposable
    {
        private readonly IOpenSSLCrypto openSSLCrypto;
        private readonly object cryptProtectLock = new object();
        private readonly IntPtr iv;
        private readonly int keySize;
        private readonly int ivSize;
        private readonly int blockSize;
        private readonly IntPtr evpCipher;
        private readonly SystemInterface systemInterface;
        private IntPtr encryptCtx;
        private IntPtr decryptCtx;
        private IntPtr key;
        private bool disposedValue;

        public OpenSSLCryptProtectMemory(string cipher, SystemInterface systemInterface, IOpenSSLCrypto openSSLCrypto)
        {
            this.openSSLCrypto = openSSLCrypto ?? throw new ArgumentNullException(nameof(openSSLCrypto));
            this.systemInterface = systemInterface;

            evpCipher = openSSLCrypto.EVP_get_cipherbyname(cipher);
            openSSLCrypto.CheckResult(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = openSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            keySize = openSSLCrypto.EVP_CIPHER_key_length(evpCipher);
            Debug.WriteLine("Key length: " + keySize);

            ivSize = openSSLCrypto.EVP_CIPHER_iv_length(evpCipher);
            Debug.WriteLine("IV length: " + ivSize);

            key = systemInterface.PageAlloc((ulong)systemInterface.PageSize);
            Check.IntPtr(key, "PageAlloc");

            systemInterface.LockMemory(key, (ulong)systemInterface.PageSize);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                systemInterface.SetNoDump(key, (ulong)systemInterface.PageSize);
            }

            iv = IntPtr.Add(key, keySize);

            Debug.WriteLine("EVP_CIPHER_CTX_new encryptCtx");
            encryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            openSSLCrypto.CheckResult(encryptCtx, "EVP_CIPHER_CTX_new");

            Debug.WriteLine("EVP_CIPHER_CTX_new decryptCtx");
            decryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            openSSLCrypto.CheckResult(decryptCtx, "EVP_CIPHER_CTX_new");

            var result = openSSLCrypto.RAND_bytes(key, keySize);
            openSSLCrypto.CheckResult(result, 1, "RAND_bytes");

            result = openSSLCrypto.RAND_bytes(iv, ivSize);
            openSSLCrypto.CheckResult(result, 1, "RAND_bytes");
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

        public ulong GetEncryptedMemoryBlockSize()
        {
            return (ulong)GetBlockSize();
        }

        public void ProcessEncryptMemory(IntPtr pointer, ulong length)
        {
            int length1 = (int)length;
            Check.IntPtr(pointer, "CryptProtectMemory");
            if (length1 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length1), length1, "CryptProtectMemory length must be > 0");
            }

            PrintIntPtr($"CryptProtectMemory({pointer}, {length1}) ", pointer, length1);

            if (disposedValue)
            {
                throw new Exception("Called CryptProtectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            ulong tmpBufferLen = (ulong)(length1 + blockSize);
            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + tmpBufferLen);
            IntPtr tmpBuffer = Marshal.AllocHGlobal((int)tmpBufferLen);
            try
            {
                systemInterface.LockMemory(tmpBuffer, tmpBufferLen);
                systemInterface.SetNoDump(tmpBuffer, tmpBufferLen);

                lock (cryptProtectLock)
                {
                    int finalOutputLength;
                    systemInterface.SetReadAccess(key, (ulong)systemInterface.PageSize);
                    try
                    {
                        int result;
                        int outputLength;
                        Debug.WriteLine("EVP_EncryptInit_ex");
                        Check.IntPtr(encryptCtx, "CryptProtectMemory encryptCtx");
                        Check.IntPtr(key, "CryptProtectMemory key");
                        Check.IntPtr(iv, "CryptProtectMemory iv");
                        try
                        {
                            PrintIntPtr("IV: ", iv, ivSize);
                            result = openSSLCrypto.EVP_EncryptInit_ex(encryptCtx, evpCipher, IntPtr.Zero, key, iv);
                            openSSLCrypto.CheckResult(result, 1, "EVP_EncryptInit_ex");

                            Debug.WriteLine("EVP_EncryptUpdate");
                            result = openSSLCrypto.EVP_EncryptUpdate(
                                encryptCtx,
                                tmpBuffer,
                                out outputLength,
                                pointer,
                                length1);
                            Check.Result(result, 1, "EVP_EncryptUpdate");

                            Debug.WriteLine($"EVP_EncryptUpdate outputLength = {outputLength}");

                            IntPtr finalOutput = IntPtr.Add(tmpBuffer, outputLength);

                            Debug.WriteLine("EVP_EncryptFinal_ex");
                            result = openSSLCrypto.EVP_EncryptFinal_ex(encryptCtx, finalOutput, out finalOutputLength);
                            openSSLCrypto.CheckResult(result, 1, "EVP_EncryptFinal_ex");
                        }
                        finally
                        {
                            result = openSSLCrypto.EVP_CIPHER_CTX_reset(encryptCtx);
                            openSSLCrypto.CheckResult(result, 1, "EVP_CIPHER_CTX_reset");
                        }

                        openSSLCrypto.CheckResult(result, 1, "EVP_EncryptFinal_ex");
                        finalOutputLength += outputLength;
                        Debug.WriteLine($"EVP_EncryptFinal_ex outputLength = {finalOutputLength}");
                        PrintIntPtr("CryptProtectMemory output ", tmpBuffer, finalOutputLength);
                    }
                    finally
                    {
                        systemInterface.SetNoAccess(key, (ulong)systemInterface.PageSize);
                    }

                    systemInterface.CopyMemory(tmpBuffer, pointer, (ulong)finalOutputLength);
                }
            }
            finally
            {
                Debug.WriteLine("FreeHGlobal");
                Marshal.FreeHGlobal(tmpBuffer);
            }
        }

        public void ProcessDecryptMemory(IntPtr pointer, ulong length)
        {
            Check.IntPtr(pointer, "CryptUnprotectMemory");
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "CryptUnprotectMemory length must be > 0");
            }

            /*
            if (length1 % blockSize != 0)
            {
                throw new ArgumentException($"CryptUnprotectMemory length must be multiple of blockSize {blockSize}", nameof(length1));
            }
            */
            PrintIntPtr($"CryptUnprotectMemory({pointer}, {length})", pointer, (int)length);

            if (disposedValue)
            {
                throw new Exception("Called CryptUnprotectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + length + blockSize);
            IntPtr tmpBuffer = Marshal.AllocHGlobal((int)length + blockSize);
            try
            {
                systemInterface.LockMemory(tmpBuffer, length + (ulong)blockSize);
                systemInterface.SetNoDump(tmpBuffer, length + (ulong)blockSize);

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
                        int decryptedLength;
                        int result;
                        try
                        {
                            PrintIntPtr("IV: ", iv, ivSize);
                            result = openSSLCrypto.EVP_DecryptInit_ex(decryptCtx, evpCipher, IntPtr.Zero, key, iv);
                            openSSLCrypto.CheckResult(result, 1, "EVP_DecryptInit_ex");

                            Debug.WriteLine("EVP_DecryptUpdate");
                            result = openSSLCrypto.EVP_DecryptUpdate(
                                decryptCtx,
                                tmpBuffer,
                                out decryptedLength,
                                pointer,
                                (int)length);

                            openSSLCrypto.CheckResult(result, 1, "EVP_DecryptUpdate");
                            Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                            PrintIntPtr("EVP_DecryptUpdate", tmpBuffer, decryptedLength);

                            IntPtr finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                            Debug.WriteLine("EVP_DecryptFinal_ex");
                            result = openSSLCrypto.EVP_DecryptFinal_ex(
                                decryptCtx,
                                finalDecrypted,
                                out finalDecryptedLength);
                            Debug.WriteLine("EVP_DecryptFinal_ex returned " + result);

                            // TODO: EVP_DecryptFinal_ex is returning 1 even though we're successfully done?
                            // openSSLCrypto.CheckResult(result, 1, "EVP_DecryptFinal_ex");
                        }
                        finally
                        {
                            openSSLCrypto.EVP_CIPHER_CTX_reset(decryptCtx);
                        }

                        finalDecryptedLength += decryptedLength;
                        Debug.WriteLine($"EVP_DecryptFinal_ex finalDecryptedLength = {finalDecryptedLength}");
                    }
                    finally
                    {
                        systemInterface.SetNoAccess(key, (ulong)systemInterface.PageSize);
                    }

                    Debug.WriteLine("CopyMemory");
                    systemInterface.CopyMemory(tmpBuffer, pointer, (ulong)finalDecryptedLength);
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
                    SystemInterface disposeSystemInterface;
                    if (disposing)
                    {
                        disposeSystemInterface = systemInterface;
                        Monitor.Enter(cryptProtectLock);
                    }
                    else
                    {
                        // NOTE: GetExistingInstance is only used in finalizer
                        disposeSystemInterface = SystemInterface.GetExistingInstance();
                    }

                    if (openSSLCrypto == null)
                    {
                        throw new Exception("Can't dispose without openSSLCrypto!");
                    }

                    if (encryptCtx != IntPtr.Zero)
                    {
                        Debug.WriteLine("EVP_CIPHER_CTX_free encryptCtx");
                        openSSLCrypto.EVP_CIPHER_CTX_free(encryptCtx);
                        encryptCtx = IntPtr.Zero;
                    }

                    if (decryptCtx != IntPtr.Zero)
                    {
                        Debug.WriteLine("EVP_CIPHER_CTX_free decryptCtx");
                        openSSLCrypto.EVP_CIPHER_CTX_free(decryptCtx);
                        decryptCtx = IntPtr.Zero;
                    }

                    if (key != IntPtr.Zero)
                    {
                        Debug.WriteLine($"PageFree({key}, {disposeSystemInterface.PageSize})");
                        disposeSystemInterface.PageFree(key, (ulong)disposeSystemInterface.PageSize);
                        key = IntPtr.Zero;
                    }
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

        [Conditional("DEBUG")]
        private void PrintIntPtr(string desc, IntPtr pointer, int length)
        {
            var sb = new System.Text.StringBuilder(" IntPtr { ");
            for (var i = 0; i < length; i++)
            {
                sb.Append(Marshal.ReadByte(pointer, i) + ", ");
            }

            sb.Append("}");
            Debug.WriteLine(desc + sb);
        }
    }
}
