using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using App.Metrics.Timer;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto.BufferUtils;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    /// <summary>
    /// Uses the AWS Key Management Service to provide an implementation of <see cref="KeyManagementService"/>. It
    /// provides multi-region support, i.e. you can encrypt data in one region and decrypt it using the keys from
    /// another region.
    /// The message format is:
    /// <code>
    /// {
    ///   "encryptedKey": base64_encoded_bytes,
    ///   "kmsKeks": [
    ///     {
    ///       "region": "aws_region",
    ///       "arn": "arn",
    ///       "encryptedKek": "base64_encoded_bytes"
    ///     },
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// </summary>
    [Obsolete("Use the Kms.KeyManagementService from the GoDaddy.Asherah.AppEncryption.PlugIns.Aws package instead. This will be removed in a future release.")]
    public class AwsKeyManagementServiceImpl : KeyManagementService
    {
        internal const string EncryptedKey = "encryptedKey";
        internal const string KmsKeksKey = "kmsKeks";
        internal const string RegionKey = "region";
        internal const string ArnKey = "arn";
        internal const string EncryptedKek = "encryptedKek";

        private readonly ILogger _logger;

        private static readonly TimerOptions EncryptkeyTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".kms.aws.encryptkey" };
        private static readonly TimerOptions DecryptkeyTimerOptions = new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".kms.aws.decryptkey" };

        private readonly string preferredRegion;
        private readonly AeadEnvelopeCrypto crypto;
        private readonly AwsKmsClientFactory awsKmsClientFactory; // leaving here in case we later decide to create dynamically
        private readonly Comparison<string> regionPriorityComparator;

        internal AwsKeyManagementServiceImpl(
            Dictionary<string, string> regionToArnDictionary,
            string preferredRegion,
            AeadEnvelopeCrypto crypto,
            AwsKmsClientFactory awsKmsClientFactory,
            AWSCredentials credentials,
            ILogger logger)
        {
            regionPriorityComparator = (region1, region2) =>
            {
                // Give preferred region top priority and fall back to remaining priority
                if (region1.Equals(this.preferredRegion, StringComparison.Ordinal))
                {
                    return -1;
                }

                if (region2.Equals(this.preferredRegion, StringComparison.Ordinal))
                {
                    return 1;
                }

                // Treat them as equal for now
                // TODO consider adding logic to prefer geo/adjacent regions
                return 0;
            };
            this.preferredRegion = preferredRegion;
            this.crypto = crypto;
            this.awsKmsClientFactory = awsKmsClientFactory;
            this._logger = logger;
            RegionToArnAndClientDictionary = new OrderedDictionary();

            List<KeyValuePair<string, string>> regionToArnList = regionToArnDictionary.ToList();
            regionToArnList.Sort((regionToArn1, regionToArn2) =>
                regionPriorityComparator(regionToArn1.Key, regionToArn2.Key));

            regionToArnList.ForEach(regionToArn =>
            {
                RegionToArnAndClientDictionary.Add(
                    regionToArn.Key,
                    new AwsKmsArnClient(regionToArn.Value, this.awsKmsClientFactory.CreateAwsKmsClient(regionToArn.Key, credentials)));
            });
        }



        public interface IBuildStep
        {
            /// <summary>
            /// Used to define custom AWS credentials for the KMS client.
            /// </summary>
            ///
            /// <param name="credentials">The custom AWS credentials to use.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithCredentials(AWSCredentials credentials);

            /// <summary>
            /// Set the logger for the <see cref="AwsKeyManagementServiceImpl"/>.
            /// </summary>
            ///
            /// <param name="logger">The logger implementation to use.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            IBuildStep WithLogger(ILogger logger);

            /// <summary>
            /// Builds the finalized <see cref="AwsKeyManagementServiceImpl"/> with the parameters specified in the builder.
            /// </summary>
            ///
            /// <returns>The fully instantiated <see cref="AwsKeyManagementServiceImpl"/> object.</returns>
            AwsKeyManagementServiceImpl Build();
        }

        internal OrderedDictionary RegionToArnAndClientDictionary { get; }

        /// <summary>
        /// Initializes a new builder for <see cref="AwsKeyManagementServiceImpl"/> using the provided parameters.
        /// </summary>
        ///
        /// <param name="regionToArnDictionary">A dictionary with region and arn of the KMS key(s) as key value pairs.
        /// </param>
        /// <param name="region">Preferred region to use.</param>
        /// <returns></returns>
        public static Builder NewBuilder(Dictionary<string, string> regionToArnDictionary, string region)
        {
            return new Builder(regionToArnDictionary, region);
        }

        /// <inheritdoc />
        public override byte[] EncryptKey(CryptoKey key)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(EncryptkeyTimerOptions))
            {
                Json kmsKeyEnvelope = new Json();

                // We generate a KMS datakey (plaintext and encrypted) and encrypt its plaintext key against remaining regions.
                // This allows us to be able to decrypt from any of the regions locally later.
                GenerateDataKeyResponse dataKey = GenerateDataKey(RegionToArnAndClientDictionary, out string dateKeyKeyId);
                byte[] dataKeyPlainText = dataKey.Plaintext.GetBuffer();

                try
                {
                    byte[] encryptedKey = crypto.EncryptKey(key, crypto.GenerateKeyFromBytes(dataKeyPlainText));
                    kmsKeyEnvelope.Put(EncryptedKey, encryptedKey);

                    ConcurrentBag<JObject> kmsRegionKeyJsonBag = new ConcurrentBag<JObject>();
                    Parallel.ForEach(RegionToArnAndClientDictionary.Cast<object>(), regionToArnClientObject =>
                    {
                        DictionaryEntry regionToArnAndClient = (DictionaryEntry)regionToArnClientObject;
                        AwsKmsArnClient arnClient = (AwsKmsArnClient)regionToArnAndClient.Value;
                        string region = (string)regionToArnAndClient.Key;
                        if (!arnClient.Arn.Equals(dateKeyKeyId, StringComparison.Ordinal))
                        {
                            // If the ARN is different than the datakey's, call encrypt since it's another region
                            EncryptKeyAndBuildResult(
                                arnClient.AwsKmsClient,
                                region,
                                arnClient.Arn,
                                dataKeyPlainText).IfSome(encryptedKeyResult => kmsRegionKeyJsonBag.Add(encryptedKeyResult));
                        }
                        else
                        {
                            // This is the datakey, so build kmsKey json for it
                            kmsRegionKeyJsonBag.Add(Option<JObject>.Some(BuildKmsRegionKeyJson(
                                region,
                                dateKeyKeyId,
                                dataKey.CiphertextBlob.GetBuffer())).IfNone(() => null));
                        }
                    });

                    // TODO Consider adding minimum or quorum check on number of entries
                    kmsKeyEnvelope.Put(KmsKeksKey, kmsRegionKeyJsonBag.ToList());
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Unexpected execution exception while encrypting KMS data key");
                    throw new AppEncryptionException("unexpected execution error during encrypt", e);
                }
                finally
                {
                    ManagedBufferUtils.WipeByteArray(dataKeyPlainText);
                }

                return kmsKeyEnvelope.ToUtf8();
            }
        }

        /// <inheritdoc />
        public override CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked)
        {
            using (MetricsUtil.MetricsInstance.Measure.Timer.Time(DecryptkeyTimerOptions))
            {
                Json kmsKeyEnvelope = new Json(keyCipherText);
                byte[] encryptedKey = kmsKeyEnvelope.GetBytes(EncryptedKey);

                foreach (Json kmsRegionKeyJson in GetPrioritizedKmsRegionKeyJsonList(kmsKeyEnvelope.GetJsonArray(KmsKeksKey)))
                {
                    string region = kmsRegionKeyJson.GetString(RegionKey);
                    if (!RegionToArnAndClientDictionary.Contains(region))
                    {
                        _logger?.LogWarning("Failed to decrypt due to no client for region {Region}, trying next region", region);
                        continue;
                    }

                    byte[] kmsKeyEncryptionKey = kmsRegionKeyJson.GetBytes(EncryptedKek);
                    try
                    {
                        TimerOptions decryptTimerOptions =
                            new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".kms.aws.decrypt." + region };
                        using (MetricsUtil.MetricsInstance.Measure.Timer.Time(decryptTimerOptions))
                        {
                            return DecryptKmsEncryptedKey(
                                ((AwsKmsArnClient)RegionToArnAndClientDictionary[region]).AwsKmsClient,
                                encryptedKey,
                                keyCreated,
                                kmsKeyEncryptionKey,
                                revoked);
                        }
                    }
                    catch (AmazonServiceException e)
                    {
                        _logger?.LogWarning(e, "Failed to decrypt via region {Region} KMS, trying next region", region);

                        // TODO Consider adding notification/CW alert
                    }
                }

                throw new KmsException("could not successfully decrypt key using any regions");
            }
        }

        internal virtual Option<JObject> EncryptKeyAndBuildResult(
            IAmazonKeyManagementService kmsClient,
            string region,
            string arn,
            byte[] dataKeyPlainText)
        {
            try
            {
                TimerOptions encryptTimerOptions =
                    new TimerOptions { Name = MetricsUtil.AelMetricsPrefix + ".kms.aws.encrypt." + region };
                using (MetricsUtil.MetricsInstance.Measure.Timer.Time(encryptTimerOptions))
                {
                    // Note: plaintext key wiping is handled by the caller since this buffer is shared
                    // Create a new MemoryStream from the plaintext
                    using (MemoryStream plaintextStream = new MemoryStream(dataKeyPlainText))
                    {
                        EncryptRequest encryptRequest = new EncryptRequest
                        {
                            KeyId = arn,
                            Plaintext = plaintextStream,
                        };

                        // Execute the request
                        EncryptResponse encryptResponse;
                        encryptResponse = kmsClient.EncryptAsync(encryptRequest).Result;

                        // Process the response - ciphertext doesn't need wiping
                        using (MemoryStream ciphertextStream = encryptResponse.CiphertextBlob)
                        {
                            // Get the ciphertext bytes
                            byte[] ciphertextBytes = new byte[ciphertextStream.Length];
                            ciphertextStream.Position = 0;
                            ciphertextStream.Read(ciphertextBytes, 0, ciphertextBytes.Length);

                            // Create the region key JSON
                            return Option<JObject>.Some(BuildKmsRegionKeyJson(region, arn, ciphertextBytes));
                        }
                    }
                }
            }
            catch (AggregateException e)
            {
                _logger?.LogWarning(e, "Failed to encrypt generated data key via region {Region} KMS", region);

                // TODO Consider adding notification/CW alert
                return Option<JObject>.None;
            }
        }

        /// <summary>
        /// Attempt to generate a KMS datakey using the first successful response using a sorted dictionary of available
        /// KMS clients.
        /// </summary>
        ///
        /// <param name="sortedRegionToArnAndClientDictionary">A sorted dictionary mapping regions and their arns and
        /// kms clients.</param>
        /// <param name="dateKeyKeyId">The KMS arn used to generate the data key</param>
        /// <returns>A GenerateDataKeyResponse object that contains the plain text key and the ciphertext for that key.
        /// </returns>
        /// <exception cref="KmsException">Throw an exception if we're unable to generate a datakey in any AWS region.
        /// </exception>
        internal virtual GenerateDataKeyResponse GenerateDataKey(OrderedDictionary sortedRegionToArnAndClientDictionary, out string dateKeyKeyId)
        {
            // Initialize output parameter
            dateKeyKeyId = null;

            foreach (DictionaryEntry regionToArnAndClient in sortedRegionToArnAndClientDictionary)
            {
                string region = (string)regionToArnAndClient.Key;
                try
                {
                    TimerOptions generateDataKeyTimerOptions = new TimerOptions
                    {
                        Name = MetricsUtil.AelMetricsPrefix + ".kms.aws.generatedatakey." + region,
                    };
                    using (MetricsUtil.MetricsInstance.Measure.Timer.Time(generateDataKeyTimerOptions))
                    {
                        IAmazonKeyManagementService client = ((AwsKmsArnClient)regionToArnAndClient.Value).AwsKmsClient;
                        string keyIdForDataKeyGeneration = ((AwsKmsArnClient)regionToArnAndClient.Value).Arn;
                        GenerateDataKeyRequest request = new GenerateDataKeyRequest
                        {
                            KeyId = keyIdForDataKeyGeneration,
                            KeySpec = DataKeySpec.AES_256,
                        };

                        GenerateDataKeyResponse generateDataKeyResult = client.GenerateDataKeyAsync(request).GetAwaiter().GetResult();
                        dateKeyKeyId = keyIdForDataKeyGeneration;
                        return generateDataKeyResult;
                    }
                }
                catch (AmazonServiceException e)
                {
                    _logger?.LogWarning(e, "Failed to generate data key via region {Region} KMS, trying next region", region);

                    // TODO Consider adding notification/CW alert
                }
            }

            throw new KmsException("could not successfully generate data key using any regions");
        }

        /// <summary>
        /// Gets an ordered list of KMS region key json objects to use. Uses preferred region and falls back to others
        /// as appropriate.
        /// </summary>
        ///
        /// <param name="kmsRegionKeyArray">A non-prioritized array of KMS region key objects.</param>
        /// <returns>A list of KMS region key json objects, prioritized by regions.</returns>
        internal List<Json> GetPrioritizedKmsRegionKeyJsonList(JArray kmsRegionKeyArray)
        {
            List<Json> kmsRegionKeyList = kmsRegionKeyArray.Map(obj => new Json((JObject)obj)).ToList();
            kmsRegionKeyList.Sort((kmsRegionKeyJson1, kmsRegionKeyJson2)
                => regionPriorityComparator(kmsRegionKeyJson1.GetString(RegionKey), kmsRegionKeyJson1.GetString(RegionKey)));
            return kmsRegionKeyList;
        }

        internal virtual CryptoKey DecryptKmsEncryptedKey(
            IAmazonKeyManagementService awsKmsClient,
            byte[] cipherText,
            DateTimeOffset keyCreated,
            byte[] kmsKeyEncryptionKey,
            bool revoked)
        {
            DecryptResponse response;
            byte[] plaintextBackingBytes;

            // Create a MemoryStream that we will dispose properly
            using (MemoryStream ciphertextBlobStream = new MemoryStream(kmsKeyEncryptionKey))
            {
                DecryptRequest request = new DecryptRequest
                {
                    CiphertextBlob = ciphertextBlobStream,
                };

                response = awsKmsClient.DecryptAsync(request).Result;
            }

            // Use proper disposal of the response plaintext stream and securely handle the sensitive bytes
            using (MemoryStream plaintextStream = response.Plaintext)
            {
                // Extract the plaintext bytes so we can wipe them in case of an exception
                plaintextBackingBytes = plaintextStream.GetBuffer();

                try
                {
                    return crypto.DecryptKey(cipherText, keyCreated, crypto.GenerateKeyFromBytes(plaintextBackingBytes), revoked);
                }
                finally
                {
                    ManagedBufferUtils.WipeByteArray(plaintextBackingBytes);
                }
            }
        }

        private static JObject BuildKmsRegionKeyJson(string region, string arn, byte[] encryptedKeyEncryptionKey)
        {
            // NOTE: ARN not needed in decrypt, but storing for now in case we want to later use for encryption context, policy, etc.
            Json kmsRegionKeyJson = new Json();
            kmsRegionKeyJson.Put(RegionKey, region);
            kmsRegionKeyJson.Put(ArnKey, arn);
            kmsRegionKeyJson.Put(EncryptedKek, encryptedKeyEncryptionKey);

            return kmsRegionKeyJson.ToJObject();
        }

        public sealed class Builder : IBuildStep
        {
            private readonly Dictionary<string, string> regionToArnDictionary;
            private readonly string preferredRegion;

            private AWSCredentials credentials;
            private ILogger _logger;

            /// <summary>
            /// Initializes the builder for <see cref="AwsKeyManagementServiceImpl"/> class with the specified options.
            /// </summary>
            ///
            /// <param name="regionToArnDictionary">A dictionary with region and arn of the KMS key(s) as key value
            /// pairs.</param>
            /// <param name="region">The preferred region to choose.</param>
            public Builder(Dictionary<string, string> regionToArnDictionary, string region)
            {
                this.regionToArnDictionary = regionToArnDictionary;
                preferredRegion = region;
            }

            /// <summary>
            /// Used to define custom AWS credentials for the KMS client.
            /// </summary>
            ///
            /// <param name="credentials">The custom AWS credentials to use.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            public IBuildStep WithCredentials(AWSCredentials credentials)
            {
                this.credentials = credentials;
                return this;
            }

            /// <summary>
            /// Set the logger for the <see cref="AwsKeyManagementServiceImpl"/>.
            /// </summary>
            ///
            /// <param name="logger">The logger implementation to use.</param>
            /// <returns>The current <see cref="IBuildStep"/> instance.</returns>
            public IBuildStep WithLogger(ILogger logger)
            {
                this._logger = logger;
                return this;
            }

            /// <summary>
            /// Builds the finalized <see cref="AwsKeyManagementServiceImpl"/> object with the parameters specified in
            /// the <see cref="Builder"/>.
            /// </summary>
            ///
            /// <returns>The fully instantiated <see cref="AwsKeyManagementServiceImpl"/> object.</returns>
            public AwsKeyManagementServiceImpl Build()
            {
                return new AwsKeyManagementServiceImpl(
                    regionToArnDictionary,
                    preferredRegion,
                    new BouncyAes256GcmCrypto(),
                    new AwsKmsClientFactory(),
                    credentials,
                    _logger);
            }
        }

        private sealed class AwsKmsArnClient
        {
            public AwsKmsArnClient(string arn, IAmazonKeyManagementService awsKmsClient)
            {
                Arn = arn;
                AwsKmsClient = awsKmsClient;
            }

            internal string Arn { get; }

            internal IAmazonKeyManagementService AwsKmsClient { get; }
        }
    }
}
