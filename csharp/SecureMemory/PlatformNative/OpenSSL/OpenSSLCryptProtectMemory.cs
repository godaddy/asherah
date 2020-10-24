using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GoDaddy.Asherah.PlatformNative.OpenSSL
{
    public class OpenSSLCryptProtectMemory : CryptProtectMemory, IMemoryEncryption
    {
        private const int TagSize = 16;
        private static long counter;
        private readonly IOpenSSLCrypto openSSLCrypto;
        private readonly object encryptContextLock = new object();
        private readonly object decryptContextLock = new object();
        private readonly int ivSize;
        private readonly int blockSize;
        private readonly IntPtr evpCipher;
        private readonly SystemInterface systemInterface;

        private IntPtr encryptCtx;
        private IntPtr decryptCtx;
        private IntPtr key;
        private bool disposedValue;
        private bool tagsUnsupported;

        public OpenSSLCryptProtectMemory(string cipher, SystemInterface systemInterface, IOpenSSLCrypto openSSLCrypto)
        {
            this.openSSLCrypto = openSSLCrypto ?? throw new ArgumentNullException(nameof(openSSLCrypto));
            this.systemInterface = systemInterface;

            openSSLCrypto.ERR_load_EVP_strings();

            evpCipher = openSSLCrypto.EVP_get_cipherbyname(cipher);
            openSSLCrypto.CheckResult(evpCipher, "EVP_get_cipherbyname");
            Debug.WriteLine("OpenSSL found cipher " + cipher);

            blockSize = openSSLCrypto.EVP_CIPHER_block_size(evpCipher);
            Debug.WriteLine("Block size: " + blockSize);

            var keySize = openSSLCrypto.EVP_CIPHER_key_length(evpCipher);
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

            Debug.WriteLine("EVP_CIPHER_CTX_new encryptCtx");
            encryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            openSSLCrypto.CheckResult(encryptCtx, "EVP_CIPHER_CTX_new");

            Debug.WriteLine("EVP_CIPHER_CTX_new decryptCtx");
            decryptCtx = openSSLCrypto.EVP_CIPHER_CTX_new();
            openSSLCrypto.CheckResult(decryptCtx, "EVP_CIPHER_CTX_new");

            var result = openSSLCrypto.RAND_bytes(key, keySize);
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

        public int GetBufferSizeForAlloc(int dataLength)
        {
            // OpenSSL CryptProtectMemory needs rounding to block size plus room for nonce / iv
            return (int)RoundToBlockSize((ulong)dataLength, (ulong)blockSize) + TagSize + ivSize;
        }

        public void ProcessEncryptMemory(IntPtr pointer, ulong dataLength)
        {
            // Length passed in is the length of user data
            // Calculate the total buffer size assuming GetBufferSizeForAlloc was used when allocating
            ulong bufferLength = (ulong)GetBufferSizeForAlloc((int)dataLength);
            Check.IntPtr(pointer, "CryptProtectMemory");

            PrintIntPtr($"CryptProtectMemory({pointer}, {dataLength}) ", pointer, (int)bufferLength);

            if (disposedValue)
            {
                throw new Exception("Called CryptProtectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            IntPtr tmpBuffer = Marshal.AllocHGlobal((int)bufferLength);
            try
            {
                systemInterface.LockMemory(tmpBuffer, bufferLength);
                systemInterface.SetNoDump(tmpBuffer, bufferLength);

                lock (encryptContextLock)
                {
                    int finalOutputLength;
                    systemInterface.SetReadAccess(key, (ulong)systemInterface.PageSize);
                    try
                    {
                        int result;
                        int outputLength;
                        Check.IntPtr(encryptCtx, "CryptProtectMemory encryptCtx");
                        Check.IntPtr(key, "CryptProtectMemory key");
                        try
                        {
                            // Generate a nonce in the input pointer's nonce/iv space
                            IntPtr iv = IntPtr.Add(pointer, (int)(bufferLength - (ulong)ivSize));
#if USE_RANDOM_NONCE
                            result = openSSLCrypto.RAND_bytes(iv, ivSize);
                            openSSLCrypto.CheckResult(result, 1, "RAND_bytes");
#else
                            Marshal.WriteInt64(iv, Interlocked.Increment(ref counter));
#endif
                            PrintIntPtr("IV: ", iv, ivSize);
                            Debug.WriteLine("EVP_EncryptInit_ex");
                            result = openSSLCrypto.EVP_EncryptInit_ex(encryptCtx, evpCipher, IntPtr.Zero, key, iv);
                            openSSLCrypto.CheckResult(result, 1, "EVP_EncryptInit_ex");

                            Debug.WriteLine("EVP_EncryptUpdate");
                            result = openSSLCrypto.EVP_EncryptUpdate(
                                encryptCtx,
                                tmpBuffer,
                                out outputLength,
                                pointer,
                                (int)dataLength);
                            Check.Result(result, 1, "EVP_EncryptUpdate");

                            Debug.WriteLine($"EVP_EncryptUpdate outputLength = {outputLength}");

                            IntPtr finalOutput = IntPtr.Add(tmpBuffer, outputLength);

                            Debug.WriteLine("EVP_EncryptFinal_ex");
                            result = openSSLCrypto.EVP_EncryptFinal_ex(encryptCtx, finalOutput, out finalOutputLength);
                            openSSLCrypto.CheckResult(result, 1, "EVP_EncryptFinal_ex");
                            IntPtr tag = IntPtr.Subtract(iv, TagSize);
                            if (!tagsUnsupported)
                            {
                                result = openSSLCrypto.EVP_CIPHER_CTX_ctrl(
                                    encryptCtx,
                                    OpenSSLConstants.EVP_CTRL_AEAD_GET_TAG,
                                    TagSize,
                                    tag);

                                if (result != 1)
                                {
                                    tagsUnsupported = true;
                                }
                            }

                            PrintIntPtr("Tag: ", tag, TagSize);
                        }
                        finally
                        {
                            result = openSSLCrypto.EVP_CIPHER_CTX_reset(encryptCtx);
                            openSSLCrypto.CheckResult(result, 1, "EVP_CIPHER_CTX_reset");
                        }

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

        public void ProcessDecryptMemory(IntPtr pointer, ulong dataLength)
        {
            // Length passed in is the length of user data
            // Calculate the total buffer size assuming GetBufferSizeForAlloc was used when allocating
            ulong bufferLength = (ulong)GetBufferSizeForAlloc((int)dataLength);
            Check.IntPtr(pointer, "CryptUnprotectMemory");

            PrintIntPtr($"CryptUnprotectMemory({pointer}, {dataLength})", pointer, (int)bufferLength);

            if (disposedValue)
            {
                throw new Exception("Called CryptUnprotectMemory on disposed OpenSSLCryptProtectMemory object");
            }

            Debug.WriteLine("AllocHGlobal for tmpBuffer: " + bufferLength);
            IntPtr tmpBuffer = Marshal.AllocHGlobal((int)bufferLength);
            try
            {
                systemInterface.LockMemory(tmpBuffer, bufferLength);
                systemInterface.SetNoDump(tmpBuffer, bufferLength);

                lock (decryptContextLock)
                {
                    int finalDecryptedLength;
                    systemInterface.SetReadAccess(key, (ulong)systemInterface.PageSize);
                    try
                    {
                        Debug.WriteLine("EVP_DecryptInit_ex");
                        Check.IntPtr(decryptCtx, "CryptUnprotectMemory decryptCtx is invalid");
                        Check.IntPtr(key, "CryptUnprotectMemory key is invalid");
                        int decryptedLength;
                        try
                        {
                            IntPtr iv = IntPtr.Add(pointer, (int)(bufferLength - (ulong)ivSize));
                            PrintIntPtr("IV: ", iv, ivSize);
                            var result = openSSLCrypto.EVP_DecryptInit_ex(decryptCtx, evpCipher, IntPtr.Zero, key, iv);
                            openSSLCrypto.CheckResult(result, 1, "EVP_DecryptInit_ex");

                            Debug.WriteLine("EVP_DecryptUpdate");
                            result = openSSLCrypto.EVP_DecryptUpdate(
                                decryptCtx,
                                tmpBuffer,
                                out decryptedLength,
                                pointer,
                                (int)RoundToBlockSize(dataLength, (ulong)blockSize));

                            openSSLCrypto.CheckResult(result, 1, "EVP_DecryptUpdate");
                            Debug.WriteLine($"EVP_DecryptUpdate decryptedLength = {decryptedLength}");

                            PrintIntPtr("EVP_DecryptUpdate", tmpBuffer, decryptedLength);
                            IntPtr tag = IntPtr.Subtract(iv, TagSize);
                            PrintIntPtr("Tag: ", tag, TagSize);
                            if (!tagsUnsupported)
                            {
                                result = openSSLCrypto.EVP_CIPHER_CTX_ctrl(
                                    decryptCtx,
                                    OpenSSLConstants.EVP_CTRL_AEAD_SET_TAG,
                                    TagSize,
                                    tag);
                                openSSLCrypto.CheckResult(result, 1, "EVP_CIPHER_CTX_ctrl");
                            }

                            IntPtr finalDecrypted = IntPtr.Add(tmpBuffer, decryptedLength);
                            Debug.WriteLine("EVP_DecryptFinal_ex");
                            result = openSSLCrypto.EVP_DecryptFinal_ex(
                                decryptCtx,
                                finalDecrypted,
                                out finalDecryptedLength);
                            Debug.WriteLine("EVP_DecryptFinal_ex returned " + result);
                            openSSLCrypto.CheckResult(result, 1, "EVP_DecryptFinal_ex");
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
                        Monitor.Enter(encryptContextLock);
                        Monitor.Enter(decryptContextLock);
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
                        Monitor.Exit(decryptContextLock);
                        Monitor.Exit(encryptContextLock);
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
