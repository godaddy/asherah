using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.Serialization;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Envelope;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Logging;

using MetastoreKeyMeta = GoDaddy.Asherah.AppEncryption.Metastore.KeyMeta;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// Internal implementation of <see cref="IEnvelopeEncryption{T}"/> that uses byte[] as the Data Row Record format.
    /// This class will eventually replace the current EnvelopeEncryptionBytesImpl to support the new IKeyMetastore integration.
    /// </summary>
    internal sealed class EnvelopeEncryption : IEnvelopeEncryption<byte[]>
    {
        private static readonly JsonSerializerOptions JsonReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = {
                new InterfaceConverter<DataRowRecordKey, IKeyRecord>(),
                new InterfaceConverter<MetastoreKeyMeta, IKeyMeta>(),
                new UnixTimestampDateTimeOffsetConverter()
            }
        };

        private static readonly JsonSerializerOptions JsonWriteOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new UnixTimestampDateTimeOffsetConverter()
            }
        };

        private readonly ISessionPartition _partition;
        private readonly IKeyMetastore _metastore;
        private readonly IKeyManagementService _keyManagementService;
        private readonly IEnvelopeCryptoContext _cryptoContext;
        private readonly ILogger _logger;

        // Cached properties from IEnvelopeCryptoContext to avoid interface dispatch overhead
        private readonly AeadEnvelopeCrypto _crypto;
        private readonly CryptoPolicy _policy;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> _systemKeyCache;
        private readonly SecureCryptoKeyDictionary<DateTimeOffset> _intermediateKeyCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeEncryption"/> class.
        /// </summary>
        /// <param name="partition">The partition for this envelope encryption instance.</param>
        /// <param name="metastore">The metastore for storing and retrieving keys.</param>
        /// <param name="keyManagementService">Service for key management operations.</param>
        /// <param name="cryptoContext">The crypto context containing crypto, policy, and key caches.</param>
        /// <param name="logger">The logger implementation to use.</param>
        public EnvelopeEncryption(
            ISessionPartition partition,
            IKeyMetastore metastore,
            IKeyManagementService keyManagementService,
            IEnvelopeCryptoContext cryptoContext,
            ILogger logger)
        {
            _partition = partition;
            _metastore = metastore;
            _keyManagementService = keyManagementService;
            _cryptoContext = cryptoContext;
            _logger = logger;

            // Cache properties to avoid repeated interface dispatch
            _crypto = cryptoContext.Crypto;
            _policy = cryptoContext.Policy;
            _systemKeyCache = cryptoContext.SystemKeyCache;
            _intermediateKeyCache = cryptoContext.IntermediateKeyCache;
        }

        /// <inheritdoc/>
        public byte[] DecryptDataRowRecord(byte[] dataRowRecord)
        {
            return DecryptDataRowRecordAsync(dataRowRecord).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public byte[] EncryptPayload(byte[] payload)
        {
            return EncryptPayloadAsync(payload).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task<byte[]> DecryptDataRowRecordAsync(byte[] dataRowRecord)
        {
            var dataRowRecordModel = DeserializeDataRowRecord(dataRowRecord);

            if (dataRowRecordModel.Key?.ParentKeyMeta?.KeyId == null)
            {
                throw new MetadataMissingException("Could not find parentKeyMeta {IK} for dataRowKey");
            }

            if (!_partition.IsValidIntermediateKeyId(dataRowRecordModel.Key.ParentKeyMeta.KeyId))
            {
                throw new MetadataMissingException($"Intermediate key '{dataRowRecordModel.Key.ParentKeyMeta.KeyId}' does not match partition '{_partition.IntermediateKeyId}'");
            }

            // the Data property is a base64 encoded string containing the encrypted payload
            // the Key property from the DataRowRecord.Key is a base64 encoded string containing the encrypted key
            var payloadEncrypted = Convert.FromBase64String(dataRowRecordModel.Data);
            var encryptedKey = Convert.FromBase64String(dataRowRecordModel.Key.Key);

            var decryptedPayload = await WithIntermediateKeyForRead(
                dataRowRecordModel.Key.ParentKeyMeta,
                intermediateCryptoKey =>
                    _crypto.EnvelopeDecrypt(
                        payloadEncrypted,
                        encryptedKey,
                        dataRowRecordModel.Key.Created,
                        intermediateCryptoKey));

            return decryptedPayload;
        }

        /// <inheritdoc/>
        public async Task<byte[]> EncryptPayloadAsync(byte[] payload)
        {
            var result = await WithIntermediateKeyForWrite(intermediateCryptoKey => _crypto.EnvelopeEncrypt(
                payload,
                intermediateCryptoKey,
                new MetastoreKeyMeta { KeyId = _partition.IntermediateKeyId, Created = intermediateCryptoKey.GetCreated() }));

            var keyRecord = new DataRowRecordKey
            {
                Created = DateTimeOffset.UtcNow,
                Key = Convert.ToBase64String(result.EncryptedKey),
                ParentKeyMeta = result.UserState
            };

            var dataRowRecord = new DataRowRecord
            {
                Key = keyRecord,
                Data = Convert.ToBase64String(result.CipherText)
            };

            return JsonSerializer.SerializeToUtf8Bytes(dataRowRecord, JsonWriteOptions);
        }

        /// <summary>
        /// Executes a function with the intermediate key for write operations.
        /// </summary>
        /// <param name="functionWithIntermediateKey">the function to call using the decrypted intermediate key.</param>
        /// <returns>The result of the function execution.</returns>
        private async Task<T> WithIntermediateKeyForWrite<T>(Func<CryptoKey, T> functionWithIntermediateKey)
        {
            // Try to get latest from cache. If not found or expired, get latest or create
            var intermediateKey = _intermediateKeyCache.GetLast();

            if (intermediateKey == null || IsKeyExpiredOrRevoked(intermediateKey))
            {
                intermediateKey = await GetLatestOrCreateIntermediateKey();

                // Put the key into our cache if allowed
                if (_policy.CanCacheIntermediateKeys())
                {
                    try
                    {
                        intermediateKey = _intermediateKeyCache.PutAndGetUsable(intermediateKey.GetCreated(), intermediateKey);
                    }
                    catch (Exception ex)
                    {
                        DisposeKey(intermediateKey, ex);
                        throw new AppEncryptionException("Unable to update cache for Intermediate Key", ex);
                    }
                }
            }

            return ApplyFunctionAndDisposeKey(intermediateKey, functionWithIntermediateKey);
        }

        /// <summary>
        /// Executes a function with the system key for write operations.
        /// </summary>
        /// <param name="functionWithSystemKey">the function to call using the decrypted system key.</param>
        /// <returns>The result of the function execution.</returns>
        private async Task<T> WithSystemKeyForWrite<T>(Func<CryptoKey, T> functionWithSystemKey)
        {
            // Try to get latest from cache. If not found or expired, get latest or create
            var systemKey = _systemKeyCache.GetLast();
            if (systemKey == null || IsKeyExpiredOrRevoked(systemKey))
            {
                systemKey = await GetLatestOrCreateSystemKey();

                // Put the key into our cache if allowed
                if (_policy.CanCacheSystemKeys())
                {
                    try
                    {
                        var systemKeyMeta = new MetastoreKeyMeta { KeyId = _partition.SystemKeyId, Created = systemKey.GetCreated() };
                        systemKey = _systemKeyCache.PutAndGetUsable(systemKeyMeta.Created, systemKey);
                    }
                    catch (Exception ex)
                    {
                        DisposeKey(systemKey, ex);
                        throw new AppEncryptionException("Unable to update cache for SystemKey", ex);
                    }
                }
            }

            return ApplyFunctionAndDisposeKey(systemKey, functionWithSystemKey);
        }

        /// <summary>
        /// Gets the latest intermediate key or creates a new one if needed.
        /// </summary>
        /// <returns>The latest or newly created intermediate key.</returns>
        private async Task<CryptoKey> GetLatestOrCreateIntermediateKey()
        {
            // Phase 1: Try to load the latest intermediate key
            var (found, newestIntermediateKeyRecord) = await _metastore.TryLoadLatestAsync(_partition.IntermediateKeyId);

            if (found)
            {
                // If the key we just got back isn't expired, then just use it
                if (!IsKeyExpiredOrRevoked(newestIntermediateKeyRecord))
                {
                    try
                    {
                        if (newestIntermediateKeyRecord.ParentKeyMeta == null)
                        {
                            throw new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey");
                        }

                        return await WithExistingSystemKey(
                            newestIntermediateKeyRecord.ParentKeyMeta,
                            true,
                            key => DecryptKey(newestIntermediateKeyRecord, key));
                    }
                    catch (MetadataMissingException ex)
                    {
                        _logger?.LogWarning(
                            ex,
                            "The SK for the IK ({KeyId}, {Created}) is missing or in an invalid state. Will create new IK instead.",
                            _partition.IntermediateKeyId,
                            newestIntermediateKeyRecord.Created);
                    }
                }

                // If we're here, we have an expired key and will create a new one
                // Fall through to Phase 2: create new key
            }

            // Phase 2: Create new intermediate key
            var intermediateKeyCreated = _policy.TruncateToIntermediateKeyPrecision(DateTime.UtcNow);
            var intermediateKey = _crypto.GenerateKey(intermediateKeyCreated);

            try
            {
                var newIntermediateKeyRecord = await WithSystemKeyForWrite(systemCryptoKey =>
                    new KeyRecord(
                        intermediateKey.GetCreated(),
                        Convert.ToBase64String(_crypto.EncryptKey(intermediateKey, systemCryptoKey)),
                        false,
                        new MetastoreKeyMeta { KeyId = _partition.SystemKeyId, Created = systemCryptoKey.GetCreated() }));

                if (await _metastore.StoreAsync(_partition.IntermediateKeyId, newIntermediateKeyRecord.Created, newIntermediateKeyRecord))
                {
                    return intermediateKey;
                }
                else
                {
                    // Duplicate detected - dispose the key we created
                    DisposeKey(intermediateKey, null);
                }
            }
            catch (Exception ex)
            {
                DisposeKey(intermediateKey, ex);
                throw new AppEncryptionException("Unable to create new Intermediate Key", ex);
            }

            // If we're here, storing failed (duplicate detected). Load the actual latest key
            var (retryFound, actualLatestIntermediateKeyRecord) = await _metastore.TryLoadLatestAsync(_partition.IntermediateKeyId);

            if (retryFound)
            {
                if (actualLatestIntermediateKeyRecord.ParentKeyMeta == null)
                {
                    throw new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey");
                }

                // Decrypt and return the actual latest key
                return await WithExistingSystemKey(
                    actualLatestIntermediateKeyRecord.ParentKeyMeta,
                    true,
                    key => DecryptKey(actualLatestIntermediateKeyRecord, key));
            }
            else
            {
                throw new AppEncryptionException("IntermediateKey not present after LoadLatestKeyRecord retry");
            }
        }

        /// <summary>
        /// Executes a function with the intermediate key for read operations.
        /// </summary>
        /// <param name="intermediateKeyMeta">intermediate key meta used previously to write a DRR.</param>
        /// <param name="functionWithIntermediateKey">the function to call using the decrypted intermediate key.</param>
        /// <returns>The result of the function execution.</returns>
        private async Task<byte[]> WithIntermediateKeyForRead(
            IKeyMeta intermediateKeyMeta, Func<CryptoKey, byte[]> functionWithIntermediateKey)
        {
            var intermediateKey = _intermediateKeyCache.Get(intermediateKeyMeta.Created);

            if (intermediateKey == null)
            {
                intermediateKey = await GetIntermediateKey(intermediateKeyMeta);

                // Put the key into our cache if allowed
                if (_policy.CanCacheIntermediateKeys())
                {
                    try
                    {
                        intermediateKey = _intermediateKeyCache.PutAndGetUsable(intermediateKey.GetCreated(), intermediateKey);
                    }
                    catch (Exception ex)
                    {
                        DisposeKey(intermediateKey, ex);
                        throw new AppEncryptionException("Unable to update cache for Intermediate key", ex);
                    }
                }
            }

            return await ApplyFunctionAndDisposeKey(intermediateKey, key => Task.FromResult(functionWithIntermediateKey(key)));
        }

        /// <summary>
        /// Fetches a known intermediate key from metastore and decrypts it using its associated system key.
        /// </summary>
        ///
        /// <returns>The decrypted intermediate key.</returns>
        ///
        /// <param name="intermediateKeyMeta">The <see cref="IKeyMeta"/> of intermediate key.</param>
        /// <exception cref="MetadataMissingException">If the intermediate key is not found, or it has missing system
        /// key info.</exception>
        private async Task<CryptoKey> GetIntermediateKey(IKeyMeta intermediateKeyMeta)
        {
            var (found, intermediateKeyRecord) = await _metastore.TryLoadAsync(intermediateKeyMeta.KeyId, intermediateKeyMeta.Created);

            if (!found)
            {
                throw new MetadataMissingException($"Could not find EnvelopeKeyRecord with keyId = {intermediateKeyMeta.KeyId}, created = {intermediateKeyMeta.Created}");
            }

            if (intermediateKeyRecord.ParentKeyMeta == null)
            {
                throw new MetadataMissingException("Could not find parentKeyMeta (SK) for intermediateKey");
            }

            return await WithExistingSystemKey(
                intermediateKeyRecord.ParentKeyMeta,
                false, // treatExpiredAsMissing = false (allow expired keys)
                systemKey => DecryptKey(intermediateKeyRecord, systemKey));
        }

        /// <summary>
        /// Calls a function using a decrypted system key that was previously used.
        /// </summary>
        /// <typeparam name="T">The type that the <paramref name="functionWithSystemKey"/> returns.</typeparam>
        ///
        /// <returns>The result returned by the <paramref name="functionWithSystemKey"/>.</returns>
        ///
        /// <param name="systemKeyMeta">system key meta used previously to write an IK.</param>
        /// <param name="treatExpiredAsMissing">if <value>true</value>, will throw a
        /// <see cref="MetadataMissingException"/> if the key is expired/revoked.</param>
        /// <param name="functionWithSystemKey">the function to call using the decrypted system key.</param>
        ///
        /// <exception cref="MetadataMissingException">If the system key is not found, or if its expired/revoked and
        /// <see cref="treatExpiredAsMissing"/> is <value>true</value>.</exception>
        private async Task<T> WithExistingSystemKey<T>(
            IKeyMeta systemKeyMeta, bool treatExpiredAsMissing, Func<CryptoKey, T> functionWithSystemKey)
        {
            // Get from cache or lookup previously used key
            var systemKey = _systemKeyCache.Get(systemKeyMeta.Created);

            if (systemKey == null)
            {
                systemKey = await GetSystemKey(systemKeyMeta);

                // Put the key into our cache if allowed
                if (_policy.CanCacheSystemKeys())
                {
                    try
                    {
                        systemKey = _systemKeyCache.PutAndGetUsable(systemKeyMeta.Created, systemKey);
                    }
                    catch (Exception ex)
                    {
                        DisposeKey(systemKey, ex);
                        throw new AppEncryptionException("Unable to update cache for SystemKey", ex);
                    }
                }
            }

            if (IsKeyExpiredOrRevoked(systemKey))
            {
                if (treatExpiredAsMissing)
                {
                    DisposeKey(systemKey, null);
                    throw new MetadataMissingException("System key is expired/revoked, keyMeta = " + systemKeyMeta);
                }
            }

            return ApplyFunctionAndDisposeKey(systemKey, functionWithSystemKey);
        }

        /// <summary>
        /// Fetches a known system key from metastore and decrypts it using the key management service.
        /// </summary>
        ///
        /// <returns>The decrypted system key.</returns>
        ///
        /// <param name="systemKeyMeta">The <see cref="IKeyMeta"/> of the system key.</param>
        /// <exception cref="MetadataMissingException">If the system key is not found.</exception>
        private async Task<CryptoKey> GetSystemKey(IKeyMeta systemKeyMeta)
        {
            var (found, systemKeyRecord) = await _metastore.TryLoadAsync(systemKeyMeta.KeyId, systemKeyMeta.Created);

            if (!found)
            {
                throw new MetadataMissingException($"Could not find EnvelopeKeyRecord with keyId = {systemKeyMeta.KeyId}, created = {systemKeyMeta.Created}");
            }

            return await _keyManagementService.DecryptKeyAsync(
                Convert.FromBase64String(systemKeyRecord.Key),
                systemKeyRecord.Created,
                systemKeyRecord.Revoked ?? false);
        }

        /// <summary>
        /// Gets the latest system key or creates a new one if needed.
        /// </summary>
        /// <returns>The latest or newly created system key.</returns>
        private async Task<CryptoKey> GetLatestOrCreateSystemKey()
        {
            // Phase 1: Load existing key
            var (found, newestSystemKeyRecord) = await _metastore.TryLoadLatestAsync(_partition.SystemKeyId);

            if (found)
            {
                // If the key we just got back isn't expired, then just use it
                if (!IsKeyExpiredOrRevoked(newestSystemKeyRecord))
                {
                    return await _keyManagementService.DecryptKeyAsync(
                        Convert.FromBase64String(newestSystemKeyRecord.Key),
                        newestSystemKeyRecord.Created,
                        newestSystemKeyRecord.Revoked ?? false);
                }

                // If we're here then we're doing inline rotation and have an expired key.
                // Fall through as if we didn't have the key
            }

            // Phase 2: Create new key
            var systemKeyCreated = _policy.TruncateToSystemKeyPrecision(DateTimeOffset.UtcNow);
            var systemKey = _crypto.GenerateKey(systemKeyCreated);
            try
            {
                var newSystemKeyRecord = new KeyRecord(
                    systemKey.GetCreated(),
                    Convert.ToBase64String(await _keyManagementService.EncryptKeyAsync(systemKey)),
                    false); // No parent key for system keys

                if (await _metastore.StoreAsync(_partition.SystemKeyId, newSystemKeyRecord.Created, newSystemKeyRecord))
                {
                    return systemKey;
                }
                else
                {
                    DisposeKey(systemKey, null);
                }
            }
            catch (Exception ex)
            {
                DisposeKey(systemKey, ex);
                throw new AppEncryptionException("Unable to store new System Key", ex);
            }

            // Phase 3: Retry logic - if storing failed, load the latest key
            var (retryFound, actualLatestSystemKeyRecord) = await _metastore.TryLoadLatestAsync(_partition.SystemKeyId);

            if (retryFound)
            {
                return await _keyManagementService.DecryptKeyAsync(
                    Convert.FromBase64String(actualLatestSystemKeyRecord.Key),
                    actualLatestSystemKeyRecord.Created,
                    actualLatestSystemKeyRecord.Revoked ?? false);
            }
            else
            {
                throw new AppEncryptionException("SystemKey not present after LoadLatestKeyRecord retry");
            }
        }

        /// <summary>
        /// Decrypts the <paramref name="keyRecord"/>'s encrypted key using the provided key.
        /// </summary>
        ///
        /// <returns>The decrypted key contained in the <paramref name="keyRecord"/>.</returns>
        ///
        /// <param name="keyRecord">The key to decrypt.</param>
        /// <param name="keyEncryptionKey">Encryption key to use for decryption.</param>
        private CryptoKey DecryptKey(IKeyRecord keyRecord, CryptoKey keyEncryptionKey)
        {
            return _crypto.DecryptKey(
                Convert.FromBase64String(keyRecord.Key),
                keyRecord.Created,
                keyEncryptionKey,
                keyRecord.Revoked ?? false);
        }

        /// <summary>
        /// Checks if a key record is expired or revoked.
        /// </summary>
        /// <param name="keyRecord">The key record to check.</param>
        /// <returns>True if the key record is expired or revoked, false otherwise.</returns>
        private bool IsKeyExpiredOrRevoked(IKeyRecord keyRecord)
        {
            return _policy.IsKeyExpired(keyRecord.Created) || (keyRecord.Revoked ?? false);
        }

        /// <summary>
        /// Checks if a key is expired or revoked.
        /// </summary>
        /// <param name="cryptoKey">The crypto key to check.</param>
        /// <returns>True if the key is expired or revoked, false otherwise.</returns>
        private bool IsKeyExpiredOrRevoked(CryptoKey cryptoKey)
        {
            return _policy.IsKeyExpired(cryptoKey.GetCreated()) || cryptoKey.IsRevoked();
        }

        /// <summary>
        /// Applies a function with a crypto key and ensures the key is properly disposed afterward.
        /// </summary>
        /// <param name="key">The crypto key to use.</param>
        /// <param name="functionWithKey">The function to execute with the key.</param>
        /// <returns>The result of the function execution.</returns>
        private static T ApplyFunctionAndDisposeKey<T>(CryptoKey key, Func<CryptoKey, T> functionWithKey)
        {
            try
            {
                return functionWithKey(key);
            }
            catch (Exception ex)
            {
                throw new AppEncryptionException($"Failed call action method, error: {ex.Message}", ex);
            }
            finally
            {
                DisposeKey(key, null);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                _cryptoContext.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected exception during dispose");
            }
        }

        /// <summary>
        /// Disposes a crypto key with proper error handling.
        /// </summary>
        /// <param name="cryptoKey">The key to dispose.</param>
        /// <param name="rootException">The root exception that caused the disposal, if any.</param>
        private static void DisposeKey(CryptoKey cryptoKey, Exception rootException)
        {
            try
            {
                cryptoKey.Dispose();
            }
            catch (Exception ex)
            {
                if (rootException != null)
                {
                    var aggregateException = new AggregateException(ex, rootException);
                    throw new AppEncryptionException(
                        $"Failed to dispose/wipe key, error: {ex.Message}", aggregateException);
                }

                throw new AppEncryptionException($"Failed to dispose/wipe key, error: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Deserializes a byte array containing UTF-8 JSON into a strongly-typed DataRowRecord.
        /// </summary>
        ///
        /// <param name="dataRowRecordBytes">The UTF-8 encoded JSON bytes representing the DataRowRecord.</param>
        /// <returns>A deserialized DataRowRecord object.</returns>
        private static DataRowRecord DeserializeDataRowRecord(byte[] dataRowRecordBytes)
        {
            if (dataRowRecordBytes == null || dataRowRecordBytes.Length == 0)
            {
                throw new ArgumentException("DataRowRecord bytes cannot be null or empty", nameof(dataRowRecordBytes));
            }

            DataRowRecord result;
            try
            {
                result = JsonSerializer.Deserialize<DataRowRecord>(dataRowRecordBytes, JsonReadOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON format in DataRowRecord bytes", nameof(dataRowRecordBytes), ex);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to deserialize DataRowRecord", nameof(dataRowRecordBytes), ex);
            }

            if (result == null)
            {
                throw new ArgumentException("Deserialized DataRowRecord cannot be null", nameof(dataRowRecordBytes));
            }

            return result;
        }
    }
}
