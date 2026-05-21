using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto.BufferUtils;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// AWS-specific implementation of <see cref="IKeyManagementService"/>.
    /// </summary>
    public sealed class KeyManagementService : IKeyManagementService, IDisposable
    {
        private readonly IReadOnlyList<KmsArnClient> _kmsArnClients;
        private readonly ILogger _logger;
        private readonly BouncyAes256GcmCrypto _crypto = new BouncyAes256GcmCrypto();

        private static readonly Action<ILogger, string, Exception> LogFailedGenerateDataKey = LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(KeyManagementService)),
            "Failed to generate data key via region {Region} KMS, trying next region");

        private static readonly Action<ILogger, Exception> LogEncryptError = LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(KeyManagementService)),
            "Unexpected execution exception while encrypting KMS data key");

        private static readonly Action<ILogger, string, Exception> LogDecryptWarning = LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(KeyManagementService)),
            "Failed to decrypt via region {Region} KMS, trying next region");

        /// <summary>
        /// Creates a new builder for KeyManagementService.
        /// </summary>
        /// <returns></returns>
        public static IKeyManagementServiceBuilder NewBuilder() => new KeyManagementServiceBuilder();

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyManagementService"/> class.
        /// </summary>
        /// <param name="kmsOptions">Key Management Service configuration options.</param>
        /// <param name="clientFactory">Factory for creating KMS clients for specific regions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public KeyManagementService(KeyManagementServiceOptions kmsOptions, IKeyManagementClientFactory clientFactory, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<KeyManagementService>();

            // Build out the KMS ARN clients
            var kmsArnClients = new List<KmsArnClient>();

            foreach (var regionKeyArn in kmsOptions.RegionKeyArns)
            {
                var client = clientFactory.CreateForRegion(regionKeyArn.Region);
                kmsArnClients.Add(new KmsArnClient(regionKeyArn.KeyArn, client, regionKeyArn.Region));
            }

            _kmsArnClients = kmsArnClients.AsReadOnly();
        }

        /// <inheritdoc/>
        public byte[] EncryptKey(CryptoKey key)
        {
            return EncryptKeyAsync(key).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            return DecryptKeyAsync(keyCipherText, keyCreated, revoked).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<CryptoKey> DecryptKeyAsync(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            var kmsKeyEnvelope = JsonSerializer.Deserialize<KmsKeyEnvelope>(keyCipherText);
            var encryptedKey = Convert.FromBase64String(kmsKeyEnvelope.EncryptedKey);

            foreach (var kmsArnClient in _kmsArnClients)
            {
                var matchingKmsKek = kmsKeyEnvelope.KmsKeks.FirstOrDefault(kek =>
                    kek.Region.Equals(kmsArnClient.Region, StringComparison.OrdinalIgnoreCase));

                if (matchingKmsKek == null)
                {
                    continue;
                }

                var kmsKeyEncryptionKey = Convert.FromBase64String(matchingKmsKek.EncryptedKek);

                try
                {
                    return await DecryptKmsEncryptedKey(kmsArnClient, encryptedKey, keyCreated, kmsKeyEncryptionKey, revoked);
                }
                catch (Exception ex)
                {
                    LogDecryptWarning(_logger, kmsArnClient.Region, ex);
                }
            }

            throw new KmsException("Could not successfully decrypt key using any regions");
        }

        /// <inheritdoc/>
        public async Task<byte[]> EncryptKeyAsync(CryptoKey key)
        {
            var (dataKey, dataKeyKeyId) = await GenerateDataKeyAsync();
            var dataKeyPlainText = dataKey.Plaintext.GetBuffer();

            try
            {
                var dataKeyCryptoKey = _crypto.GenerateKeyFromBytes(dataKeyPlainText);
                var encryptedKey = _crypto.EncryptKey(key, dataKeyCryptoKey);

                var kmsKeyEnvelope = new KmsKeyEnvelope
                {
                    EncryptedKey = Convert.ToBase64String(encryptedKey)
                };

                foreach (var kmsArnClient in _kmsArnClients)
                {
                    if (!kmsArnClient.Arn.Equals(dataKeyKeyId, StringComparison.Ordinal))
                    {
                        // If the ARN is different than the datakey's, call encrypt since it's another region
                        var kmsKek = await CreateKmsKek(kmsArnClient, dataKeyPlainText);
                        kmsKeyEnvelope.KmsKeks.Add(kmsKek);
                    }
                    else
                    {
                        // This is the datakey, so build kmsKey json for it
                        var kmsKek = new KmsKek
                        {
                            Region = kmsArnClient.Region,
                            Arn = kmsArnClient.Arn,
                            EncryptedKek = Convert.ToBase64String(dataKey.CiphertextBlob.GetBuffer())
                        };
                        kmsKeyEnvelope.KmsKeks.Add(kmsKek);
                    }
                }

                return JsonSerializer.SerializeToUtf8Bytes(kmsKeyEnvelope);
            }
            catch (Exception ex)
            {
                LogEncryptError(_logger, ex);
                throw new KmsException("unexpected execution error during encrypt");
            }
            finally
            {
                ManagedBufferUtils.WipeByteArray(dataKeyPlainText);
            }
        }

        /// <summary>
        /// Generates a KMS data key for encryption.
        /// </summary>
        /// <returns>A tuple containing the response and the key ID used for the data key.</returns>
        private async Task<(GenerateDataKeyResponse response, string dataKeyKeyId)> GenerateDataKeyAsync()
        {
            foreach (var kmsArnClient in _kmsArnClients)
            {
                try
                {
                    var request = new GenerateDataKeyRequest
                    {
                        KeyId = kmsArnClient.Arn,
                        KeySpec = DataKeySpec.AES_256,
                    };

                    var response = await kmsArnClient.Client.GenerateDataKeyAsync(request);
                    return (response, kmsArnClient.Arn);
                }
                catch (Exception ex)
                {
                    LogFailedGenerateDataKey(_logger, kmsArnClient.Region, ex);
                }
            }

            throw new KmsException("Could not successfully generate data key using any regions");
        }

        /// <summary>
        /// Decrypts a KMS encrypted key using the specified client and parameters.
        /// </summary>
        /// <param name="kmsArnClient">The KMS ARN client containing client, region, and ARN.</param>
        /// <param name="encryptedKey">The encrypted key to decrypt.</param>
        /// <param name="keyCreated">When the key was created.</param>
        /// <param name="kmsKeyEncryptionKey">The encrypted KMS key.</param>
        /// <param name="revoked">Whether the key is revoked.</param>
        /// <returns>The decrypted crypto key.</returns>
        private async Task<CryptoKey> DecryptKmsEncryptedKey(
            KmsArnClient kmsArnClient,
            byte[] encryptedKey,
            DateTimeOffset keyCreated,
            byte[] kmsKeyEncryptionKey,
            bool revoked)
        {
            DecryptResponse response;
            byte[] plaintextBackingBytes;

            // Create a MemoryStream that we will dispose properly
            using (var ciphertextBlobStream = new MemoryStream(kmsKeyEncryptionKey))
            {
                var request = new DecryptRequest
                {
                    CiphertextBlob = ciphertextBlobStream,
                };

                response = await kmsArnClient.Client.DecryptAsync(request);
            }

            // Use proper disposal of the response plaintext stream and securely handle the sensitive bytes
            using (var plaintextStream = response.Plaintext)
            {
                // Extract the plaintext bytes so we can wipe them in case of an exception
                plaintextBackingBytes = plaintextStream.GetBuffer();

                try
                {
                    return _crypto.DecryptKey(encryptedKey, keyCreated, _crypto.GenerateKeyFromBytes(plaintextBackingBytes), revoked);
                }
                finally
                {
                    ManagedBufferUtils.WipeByteArray(plaintextBackingBytes);
                }
            }
        }

        /// <summary>
        /// Encrypts a data key for a specific region and builds the result.
        /// </summary>
        /// <param name="kmsArnClient">The KMS ARN client containing client, region, and ARN.</param>
        /// <param name="dataKeyPlainText">The plaintext data key to encrypt.</param>
        /// <returns>A KmsKek object containing the encrypted result.</returns>
        private static async Task<KmsKek> CreateKmsKek(
            KmsArnClient kmsArnClient,
            byte[] dataKeyPlainText)
        {
            using (var plaintextStream = new MemoryStream(dataKeyPlainText))
            {
                var encryptRequest = new EncryptRequest
                {
                    KeyId = kmsArnClient.Arn,
                    Plaintext = plaintextStream
                };

                var encryptResponse = await kmsArnClient.Client.EncryptAsync(encryptRequest);

                // Process the response - ciphertext doesn't need wiping
                using (var ciphertextStream = encryptResponse.CiphertextBlob)
                {
                    // Get the ciphertext bytes
                    var ciphertextBytes = new byte[ciphertextStream.Length];
                    ciphertextStream.Position = 0;
                    ciphertextStream.Read(ciphertextBytes, 0, ciphertextBytes.Length);

                    // Create and return the KmsKek object
                    return new KmsKek
                    {
                        Region = kmsArnClient.Region,
                        Arn = kmsArnClient.Arn,
                        EncryptedKek = Convert.ToBase64String(ciphertextBytes)
                    };
                }
            }
        }

        /// <summary>
        /// Private class representing the KMS key envelope structure.
        /// </summary>
        private sealed class KmsKeyEnvelope
        {
            /// <summary>
            /// Gets or sets the encrypted key.
            /// </summary>
            [JsonPropertyName("encryptedKey")]
            public string EncryptedKey { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the list of KMS key encryption keys.
            /// </summary>
            [JsonPropertyName("kmsKeks")]
            public List<KmsKek> KmsKeks { get; set; } = new List<KmsKek>();
        }

        /// <summary>
        /// Private class representing a KMS key encryption key entry.
        /// </summary>
        private sealed class KmsKek
        {
            /// <summary>
            /// Gets or sets the AWS region.
            /// </summary>
            [JsonPropertyName("region")]
            public string Region { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the KMS key ARN.
            /// </summary>
            [JsonPropertyName("arn")]
            public string Arn { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the encrypted key encryption key.
            /// </summary>
            [JsonPropertyName("encryptedKek")]
            public string EncryptedKek { get; set; } = string.Empty;
        }

        /// <summary>
        /// Disposes the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            _crypto?.Dispose();
        }
    }
}
