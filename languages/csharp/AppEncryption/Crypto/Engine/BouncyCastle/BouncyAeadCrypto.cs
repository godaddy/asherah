using System;
using GoDaddy.Asherah.Crypto.BufferUtils;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using GoDaddy.Asherah.Logging;
using GoDaddy.Asherah.SecureMemory;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace GoDaddy.Asherah.Crypto.Engine.BouncyCastle
{
    public abstract class BouncyAeadCrypto : AeadEnvelopeCrypto
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<BouncyAeadCrypto>();

        public override byte[] Encrypt(byte[] input, CryptoKey key)
        {
            byte[] nonce = GenerateNonce();
            IAeadBlockCipher cipher = GetNewAeadBlockCipherInstance();
            AeadParameters cipherParameters = GetParameters(key, nonce);
            try
            {
                cipher.Init(true, cipherParameters);
                int outputLen = cipher.GetOutputSize(input.Length);
                byte[] output = new byte[outputLen + nonce.Length];
                int position = cipher.ProcessBytes(input, 0, input.Length, output, 0);

                try
                {
                    cipher.DoFinal(output, position);
                }
                catch (Exception e)
                {
                    throw new AppEncryptionException("unexpected error during encrypt cipher finalization", e);
                }

                AppendNonce(output, nonce);
                return output;
            }
            finally
            {
                ManagedBufferUtils.WipeByteArray(cipherParameters.Key.GetKey());
            }
        }

        public override byte[] Decrypt(byte[] input, CryptoKey key)
        {
            byte[] nonce = GetAppendedNonce(input);
            IAeadBlockCipher cipher = GetNewAeadBlockCipherInstance();
            AeadParameters cipherParameters = GetParameters(key, nonce);
            try
            {
                cipher.Init(false, cipherParameters);
                int cipherTextLength = input.Length - nonce.Length;
                int outputLen = cipher.GetOutputSize(cipherTextLength);
                byte[] output = new byte[outputLen];
                int position = cipher.ProcessBytes(input, 0, cipherTextLength, output, 0);

                try
                {
                    position += cipher.DoFinal(output, position);
                }
                catch (Exception e)
                {
                    throw new AppEncryptionException("unexpected error during decrypt cipher finalization", e);
                }

                if (position != outputLen)
                {
                    if (Debug.On)
                    #pragma warning disable 162
                    {
                        Logger.LogError("position {position} not equal to outputLength {outputLen}", position, outputLen);
                    }
                    #pragma warning restore 162

                    throw new AppEncryptionException("unexpected error during decrypt cipher finalization");
                }

                return output;
            }
            finally
            {
                ManagedBufferUtils.WipeByteArray(cipherParameters.Key.GetKey());
            }
        }

        protected abstract IAeadBlockCipher GetNewAeadBlockCipherInstance();

        protected abstract AeadParameters GetParameters(CryptoKey key, byte[] nonce);
    }
}
