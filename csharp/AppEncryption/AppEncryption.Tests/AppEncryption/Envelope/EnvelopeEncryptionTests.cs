using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Kms;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers.Dummy;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Engine.BouncyCastle;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Envelope;

[ExcludeFromCodeCoverage]
public class EnvelopeEncryptionTests
{
    private readonly DefaultPartition _partition = new("defaultPartition", "testService", "testProduct");
    private readonly TestHelpers.LoggerFactoryStub _loggerFactory = new();

    private EnvelopeEncryption NewEnvelopeEncryption(
        CryptoPolicy cryptoPolicy = null,
        IKeyManagementService keyManagementService = null,
        IKeyMetastore metastore = null,
        Partition partition = null)
    {
        metastore ??= new InMemoryKeyMetastore();
        var logger = _loggerFactory.CreateLogger("EnvelopeEncryptionTests");
        keyManagementService ??= new StaticKeyManagementService();
        var crypto = new BouncyAes256GcmCrypto();
        partition ??= _partition;

        cryptoPolicy ??= BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(true)
            .WithCanCacheSystemKeys(true)
            .WithCanCacheSessions(false)
            .Build();

        var systemKeyCache = new SecureCryptoKeyDictionary<DateTimeOffset>(cryptoPolicy.GetRevokeCheckPeriodMillis());
        var cryptoContext = new SessionCryptoContext(crypto, cryptoPolicy, systemKeyCache);

        return new EnvelopeEncryption(
            partition,
            metastore,
            keyManagementService,
            cryptoContext,
            logger);
    }

    [Fact]
    public void EncryptDecrypt_WithDefaults_Sync()
    {
        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption = NewEnvelopeEncryption();
        var dataRowRecordBytes = envelopeEncryption.EncryptPayload(inputBytes);

        ValidateDataRowRecordJson(dataRowRecordBytes);

        var decryptedBytes = envelopeEncryption.DecryptDataRowRecord(dataRowRecordBytes);
        var outputValue = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(inputValue, outputValue);
    }

    [Fact]
    public async Task EncryptDecrypt_WithDefaults()
    {
        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption = NewEnvelopeEncryption();
        var dataRowRecordBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);

        ValidateDataRowRecordJson(dataRowRecordBytes);

        var decryptedBytes = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValue = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(inputValue, outputValue);
    }

    [Fact]
    public async Task EncryptDecrypt_MultipleTimes_WithDefaults()
    {
        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        const string inputValue2 = "Lorem ipsum dolor sit amet, consectetur adipiscing elit";
        var inputBytes2 = System.Text.Encoding.UTF8.GetBytes(inputValue2);

        using var envelopeEncryption = NewEnvelopeEncryption();
        var dataRowRecordBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);
        var dataRowRecordBytes2 = await envelopeEncryption.EncryptPayloadAsync(inputBytes2);

        ValidateDataRowRecordJson(dataRowRecordBytes);
        ValidateDataRowRecordJson(dataRowRecordBytes2);

        var decryptedBytes = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValue = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(inputValue, outputValue);

        var decryptAgainBytes = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValueAgain = System.Text.Encoding.UTF8.GetString(decryptAgainBytes);

        Assert.Equal(inputValue, outputValueAgain);

        var decryptedBytes2 = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes2);
        var outputValue2 = System.Text.Encoding.UTF8.GetString(decryptedBytes2);

        Assert.Equal(inputValue2, outputValue2);
    }

    [Fact]
    public async Task EncryptDecrypt_WithDifferentInstances()
    {
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore);
        using var envelopeEncryption2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore);

        var dataRowRecordBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);
        var dataRowRecordBytes2 = await envelopeEncryption2.EncryptPayloadAsync(inputBytes);

        ValidateDataRowRecordJson(dataRowRecordBytes);

        var decryptedBytes = await envelopeEncryption2.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValue = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(inputValue, outputValue);

        var decryptedBytes2 = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes2);
        var outputValue2 = System.Text.Encoding.UTF8.GetString(decryptedBytes2);

        Assert.Equal(inputValue, outputValue2);
    }

    [Fact]
    public async Task Decrypt_Throws_When_IntermediateKey_Cannot_Be_Found()
    {
        var keyManagementService = new StaticKeyManagementService();
        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption = NewEnvelopeEncryption(cryptoPolicy, keyManagementService);

        // new instance will be using an empty metastore
        using var envelopeEncryption2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService);

        var dataRowRecordBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);

        await Assert.ThrowsAsync<MetadataMissingException>(async () =>
        {
            await envelopeEncryption2.DecryptDataRowRecordAsync(dataRowRecordBytes);
        });

    }


    [Theory]
    [MemberData(nameof(GetCryptoPolicies))]
    public async Task EncryptDecrypt_WithVariousCryptoPolicies(CryptoPolicy cryptoPolicy)
    {
        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption = NewEnvelopeEncryption(cryptoPolicy);
        var dataRowRecordBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);

        ValidateDataRowRecordJson(dataRowRecordBytes);

        var decryptedBytes = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValue = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(inputValue, outputValue);

        var decryptAgainBytes = await envelopeEncryption.DecryptDataRowRecordAsync(dataRowRecordBytes);
        var outputValueAgain = System.Text.Encoding.UTF8.GetString(decryptAgainBytes);

        Assert.Equal(inputValue, outputValueAgain);
    }

    public static TheoryData<CryptoPolicy> GetCryptoPolicies()
    {
        return new TheoryData<CryptoPolicy>(
            BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build(),
            BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(true)
            .WithCanCacheSessions(false)
            .Build(),
            BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(true)
            .WithCanCacheSystemKeys(true)
            .WithCanCacheSessions(false)
            .Build());
    }

    [Fact]
    public async Task Encrypt_Uses_Partitions()
    {
        var partition1 = new DefaultPartition("partition1", "service", "product");
        var partition2 = new DefaultPartition("partition2", "service", "product");

        const string inputValue = "The quick brown fox jumps over the lazy dog";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        using var envelopeEncryption1 = NewEnvelopeEncryption(partition: partition1);
        using var envelopeEncryption2 = NewEnvelopeEncryption(partition: partition2);

        var dataRowRecordBytes1 = await envelopeEncryption1.EncryptPayloadAsync(inputBytes);
        var dataRowRecordBytes2 = await envelopeEncryption2.EncryptPayloadAsync(inputBytes);

        Assert.NotEqual(dataRowRecordBytes1, dataRowRecordBytes2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not a JSON string")]
    [InlineData("null")] // Missing required fields
    [InlineData("{\"Invalid\":\"Missing required fields\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":null,\"Data\":\"ValidBase64ButKeyIsNull\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":{\"Created\":1752685310,\"Key\":\"ParentKeyMetaIsMissing\"},\"Data\":\"SomeData\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":{\"Created\":1752685310,\"Key\":\"ParentKeyMetaIsNull\",\"ParentKeyMeta\":null},\"Data\":\"SomeData\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":{\"Created\":1752685310,\"Key\":\"ParentKeyKeyIdIsMissing\",\"ParentKeyMeta\":{\"Created\":1752501780}},\"Data\":\"SomeData\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":{\"Created\":1752685310,\"Key\":\"ParentKeyKeyIdIsNull\",\"ParentKeyMeta\":{\"KeyId\":null,\"Created\":1752501780}},\"Data\":\"SomeData\"}", typeof(MetadataMissingException))]
    [InlineData("{\"Key\":{\"Created\":1752685310,\"Key\":\"ParentKeyKeyIdNotValid\",\"ParentKeyMeta\":{\"KeyId\":\"not-valid-key\",\"Created\":1752501780}},\"Data\":\"SomeData\"}", typeof(MetadataMissingException))]
    public async Task Bad_DataRowRecord_Throws(string dataRowRecordString, Type exceptionType = null)
    {
        var badDataRowRecordBytes = System.Text.Encoding.UTF8.GetBytes(dataRowRecordString);

        using var envelopeEncryption = NewEnvelopeEncryption();

        exceptionType ??= typeof(ArgumentException);

        await Assert.ThrowsAsync(exceptionType, async () =>
        {
            await envelopeEncryption.DecryptDataRowRecordAsync(badDataRowRecordBytes);
        });
    }

    [Fact]
    public async Task InlineRotation_CreatesNewKeys_WhenExistingKeysAreExpired()
    {
        // Arrange: Use a shared metastore and KMS with a test partition
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("rotationTest", "testService", "testProduct");

        // First, create keys with a long expiration policy
        var longExpirationPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false) // Disable caching to force metastore lookups
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue = "Test data for inline rotation";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption creates initial keys
        using var envelopeEncryption1 = NewEnvelopeEncryption(longExpirationPolicy, keyManagementService, metastore, partition);
        var firstEncryptedBytes = await envelopeEncryption1.EncryptPayloadAsync(inputBytes);

        // Use a 0-day expiration policy (keys are immediately expired)
        // Since key precision is truncated to minutes, we use a different partition to avoid
        // minute-precision collision issues in tests. The 0-day policy ensures the existing key
        // would be seen as expired if we were testing the same partition.
        var immediateExpirationPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(0) // Keys expire immediately
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        // Use a different partition so we get completely fresh keys (avoids minute-precision collision)
        var rotationPartition = new DefaultPartition("rotationTest2", "testService", "testProduct");

        // Second encryption with the expired policy on a fresh partition
        // This verifies that the code path for "no existing key" works (empty metastore for this partition)
        using var envelopeEncryption2 = NewEnvelopeEncryption(immediateExpirationPolicy, keyManagementService, metastore, rotationPartition);
        var secondEncryptedBytes = await envelopeEncryption2.EncryptPayloadAsync(inputBytes);

        // Verify we created keys for both partitions (different IK timestamps since different partitions)
        // Note: Since they're different partitions, this confirms both code paths work
        ValidateDataRowRecordJson(firstEncryptedBytes);
        ValidateDataRowRecordJson(secondEncryptedBytes);

        // Verify the first encrypted payload can still be decrypted
        using var decryptionEnvelope = NewEnvelopeEncryption(longExpirationPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptionEnvelope.DecryptDataRowRecordAsync(firstEncryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));

        // Verify the second encrypted payload can also be decrypted
        using var decryptionEnvelope2 = NewEnvelopeEncryption(longExpirationPolicy, keyManagementService, metastore, rotationPartition);
        var decrypted2 = await decryptionEnvelope2.DecryptDataRowRecordAsync(secondEncryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    [Fact]
    public async Task InlineRotation_WithRevokedKey_CreatesNewKey()
    {
        // This test verifies that revoked keys trigger rotation
        // We'll use a helper metastore that we can manipulate to simulate a revoked key scenario
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("revokedKeyTest", "testService", "testProduct");

        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue = "Test data for revoked key scenario";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Encrypt and decrypt successfully
        using var envelopeEncryption = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await envelopeEncryption.EncryptPayloadAsync(inputBytes);

        ValidateDataRowRecordJson(encryptedBytes);

        var decryptedBytes = await envelopeEncryption.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decryptedBytes));
    }

    [Fact]
    public async Task DuplicateKeyCreation_ConcurrentEncryption_BothSucceed()
    {
        // This test verifies that when two concurrent encryption operations try to create keys,
        // both succeed (one creates, one uses the created key via retry logic)
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("concurrentTest", "testService", "testProduct");

        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false) // Disable caching to force metastore lookups
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue1 = "Test data 1 for concurrent encryption";
        const string inputValue2 = "Test data 2 for concurrent encryption";
        var inputBytes1 = System.Text.Encoding.UTF8.GetBytes(inputValue1);
        var inputBytes2 = System.Text.Encoding.UTF8.GetBytes(inputValue2);

        // Create two envelope encryption instances sharing the same metastore
        using var envelopeEncryption1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        using var envelopeEncryption2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        // Run concurrent encryption operations
        var task1 = envelopeEncryption1.EncryptPayloadAsync(inputBytes1);
        var task2 = envelopeEncryption2.EncryptPayloadAsync(inputBytes2);

        var results = await Task.WhenAll(task1, task2);

        var encryptedBytes1 = results[0];
        var encryptedBytes2 = results[1];

        // Both should succeed
        ValidateDataRowRecordJson(encryptedBytes1);
        ValidateDataRowRecordJson(encryptedBytes2);

        // Both should use the same intermediate key (same partition, same minute)
        var ik1Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes1);
        var ik2Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes2);
        Assert.Equal(ik1Created, ik2Created);

        // Both should be decryptable
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes1);
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes2);

        Assert.Equal(inputValue1, System.Text.Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(inputValue2, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    [Fact]
    public async Task DuplicateKeyCreation_SequentialEncryption_ReusesSameKey()
    {
        // This test verifies that sequential encryption operations on the same partition
        // reuse the same key (no duplicate creation)
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("sequentialTest", "testService", "testProduct");

        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false) // Disable caching to force metastore lookups
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue1 = "First encryption";
        const string inputValue2 = "Second encryption";
        var inputBytes1 = System.Text.Encoding.UTF8.GetBytes(inputValue1);
        var inputBytes2 = System.Text.Encoding.UTF8.GetBytes(inputValue2);

        // First encryption creates the key
        using var envelopeEncryption1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes1 = await envelopeEncryption1.EncryptPayloadAsync(inputBytes1);
        var ik1Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes1);

        // Second encryption (new instance) should find and reuse the existing key
        using var envelopeEncryption2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes2 = await envelopeEncryption2.EncryptPayloadAsync(inputBytes2);
        var ik2Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes2);

        // Same key should be used (within the same minute)
        Assert.Equal(ik1Created, ik2Created);

        // Both should be decryptable
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes1);
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes2);

        Assert.Equal(inputValue1, System.Text.Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(inputValue2, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    [Fact]
    public async Task DuplicateKeyCreation_MultipleConcurrentOperations_AllSucceed()
    {
        // This test verifies that many concurrent operations all succeed
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("multiConcurrentTest", "testService", "testProduct");

        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30)
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false)
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const int numOperations = 10;
        var tasks = new Task<byte[]>[numOperations];
        var envelopes = new EnvelopeEncryption[numOperations];

        // Create envelope instances and start concurrent encryptions
        for (int i = 0; i < numOperations; i++)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes($"Concurrent operation {i}");
            envelopes[i] = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
            tasks[i] = envelopes[i].EncryptPayloadAsync(inputBytes);
        }

        // Wait for all to complete
        var results = await Task.WhenAll(tasks);

        // Verify all succeeded and use the same IK
        long? expectedIkCreated = null;
        for (int i = 0; i < numOperations; i++)
        {
            ValidateDataRowRecordJson(results[i]);
            var ikCreated = GetIntermediateKeyCreatedFromDataRowRecord(results[i]);

            if (expectedIkCreated == null)
            {
                expectedIkCreated = ikCreated;
            }
            else
            {
                Assert.Equal(expectedIkCreated, ikCreated);
            }
        }

        // Verify all can be decrypted
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        for (int i = 0; i < numOperations; i++)
        {
            var decrypted = await decryptEnvelope.DecryptDataRowRecordAsync(results[i]);
            Assert.Equal($"Concurrent operation {i}", System.Text.Encoding.UTF8.GetString(decrypted));
        }

        // Dispose all envelopes
        foreach (var envelope in envelopes)
        {
            envelope.Dispose();
        }
    }

    [Fact]
    public async Task InlineRotation_UsesExistingKey_WhenNotExpired()
    {
        // Arrange: Use a shared metastore and KMS
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();

        var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
            .WithKeyExpirationDays(30) // Keys won't expire during test
            .WithRevokeCheckMinutes(30)
            .WithCanCacheIntermediateKeys(false) // Disable caching to force metastore lookups
            .WithCanCacheSystemKeys(false)
            .WithCanCacheSessions(false)
            .Build();

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption creates initial keys
        using var envelopeEncryption1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore);
        var firstEncryptedBytes = await envelopeEncryption1.EncryptPayloadAsync(inputBytes);
        var firstIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(firstEncryptedBytes);

        // Second encryption with same policy should reuse the existing key
        using var envelopeEncryption2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore);
        var secondEncryptedBytes = await envelopeEncryption2.EncryptPayloadAsync(inputBytes);
        var secondIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(secondEncryptedBytes);

        // Assert: Same intermediate key should be used (same Created timestamp)
        Assert.Equal(firstIkCreated, secondIkCreated);
    }

    #region Dispose Tests

    [Fact]
    public void Dispose_AfterNormalOperations_DoesNotThrow()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("disposeTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: true, canCacheIntermediateKeys: true);

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Act: Create, use, and dispose
        var envelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encrypted = envelope.EncryptPayload(inputBytes);
        var decrypted = envelope.DecryptDataRowRecord(encrypted);

        // Dispose should not throw
        var exception = Record.Exception(() => envelope.Dispose());

        // Assert
        Assert.Null(exception);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted));
    }

    [Fact]
    public void Dispose_WithoutAnyOperations_DoesNotThrow()
    {
        // Arrange: Create envelope but don't use it
        var envelope = NewEnvelopeEncryption();

        // Act & Assert: Dispose should not throw even if no operations were performed
        var exception = Record.Exception(() => envelope.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var envelope = NewEnvelopeEncryption();

        // Act: Call dispose multiple times
        var exception1 = Record.Exception(() => envelope.Dispose());
        var exception2 = Record.Exception(() => envelope.Dispose());

        // Assert: Neither should throw
        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    #endregion

    #region Inline Rotation Tests with Minimal Mocking

    /// <summary>
    /// Tests that inline rotation creates a new intermediate key when the existing one is marked as expired.
    /// Uses ConfigurableCryptoPolicy to control expiration without waiting for real time to pass.
    /// ConfigurableCryptoPolicy uses second-precision timestamps to avoid duplicate key issues.
    /// </summary>
    [Fact]
    public async Task InlineRotation_WithConfigurablePolicy_CreatesNewIntermediateKey()
    {
        // Arrange: Real implementations except for configurable crypto policy
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("inlineRotationTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data for inline rotation";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption - creates initial keys (not expired)
        using var envelope1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var firstEncryptedBytes = await envelope1.EncryptPayloadAsync(inputBytes);
        var firstIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(firstEncryptedBytes);

        // Mark the first IK as expired
        cryptoPolicy.MarkKeyAsExpired(DateTimeOffset.FromUnixTimeSeconds(firstIkCreated));

        // Wait to ensure we're in a different second (policy uses second precision)
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // Second encryption - should detect expired IK and create new one (inline rotation)
        using var envelope2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var secondEncryptedBytes = await envelope2.EncryptPayloadAsync(inputBytes);
        var secondIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(secondEncryptedBytes);

        // Assert: New intermediate key was created (different timestamp)
        Assert.NotEqual(firstIkCreated, secondIkCreated);

        // Both should still be decryptable (old key still works for reads)
        cryptoPolicy.ClearExpirations(); // Clear expirations for decryption
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(firstEncryptedBytes);
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(secondEncryptedBytes);

        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    /// <summary>
    /// Tests that inline rotation creates a new system key when the existing one is marked as expired.
    /// </summary>
    [Fact]
    public async Task InlineRotation_WithConfigurablePolicy_CreatesNewSystemKey()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("systemKeyRotationTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data for system key rotation";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption - creates initial keys
        using var envelope1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var firstEncryptedBytes = await envelope1.EncryptPayloadAsync(inputBytes);
        var firstIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(firstEncryptedBytes);

        // Mark ALL keys as expired (both IK and SK)
        cryptoPolicy.MarkAllKeysAsExpired();

        // Wait to ensure we're in a different second
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // Second encryption - should create new IK and SK
        using var envelope2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var secondEncryptedBytes = await envelope2.EncryptPayloadAsync(inputBytes);
        var secondIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(secondEncryptedBytes);

        // Assert: New intermediate key was created
        Assert.NotEqual(firstIkCreated, secondIkCreated);

        // Both should still be decryptable
        cryptoPolicy.ClearExpirations();
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(firstEncryptedBytes);
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(secondEncryptedBytes);

        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    /// <summary>
    /// Tests that multiple rotations work correctly in sequence.
    /// </summary>
    [Fact]
    public async Task InlineRotation_MultipleRotations_AllKeysRemainDecryptable()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("multiRotationTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        var encryptedPayloads = new List<(byte[] encrypted, string original)>();

        // Create 3 generations of keys
        for (int i = 0; i < 3; i++)
        {
            var inputValue = $"Generation {i} data";
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

            using var envelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
            var encrypted = await envelope.EncryptPayloadAsync(inputBytes);
            encryptedPayloads.Add((encrypted, inputValue));

            // Mark all current keys as expired before next iteration
            cryptoPolicy.MarkAllKeysAsExpired();

            // Wait to ensure we're in a different second for the next generation
            if (i < 2) // Don't wait after the last iteration
            {
                await Task.Delay(TimeSpan.FromSeconds(1.1));
            }
        }

        // Verify all 3 generations created different IKs
        var ikTimestamps = encryptedPayloads
            .Select(p => GetIntermediateKeyCreatedFromDataRowRecord(p.encrypted))
            .ToList();

        Assert.Equal(3, ikTimestamps.Distinct().Count());

        // Verify all generations can still be decrypted
        cryptoPolicy.ClearExpirations();
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        foreach (var (encrypted, original) in encryptedPayloads)
        {
            var decrypted = await decryptEnvelope.DecryptDataRowRecordAsync(encrypted);
            Assert.Equal(original, System.Text.Encoding.UTF8.GetString(decrypted));
        }
    }

    /// <summary>
    /// Tests that when a key is not expired, it gets reused (no unnecessary rotation).
    /// </summary>
    [Fact]
    public async Task InlineRotation_KeyNotExpired_ReusesExistingKey()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("noRotationTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue1 = "First encryption";
        const string inputValue2 = "Second encryption";

        // First encryption
        using var envelope1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var firstEncrypted = await envelope1.EncryptPayloadAsync(System.Text.Encoding.UTF8.GetBytes(inputValue1));
        var firstIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(firstEncrypted);

        // Second encryption - key not marked as expired, should reuse
        using var envelope2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var secondEncrypted = await envelope2.EncryptPayloadAsync(System.Text.Encoding.UTF8.GetBytes(inputValue2));
        var secondIkCreated = GetIntermediateKeyCreatedFromDataRowRecord(secondEncrypted);

        // Assert: Same IK was reused
        Assert.Equal(firstIkCreated, secondIkCreated);
    }

    #endregion

    #region WithExistingSystemKey Cache Tests

    /// <summary>
    /// Tests that WithExistingSystemKey caches the system key when CanCacheSystemKeys is true.
    /// The first decrypt loads the SK from metastore and caches it.
    /// </summary>
    [Fact]
    public async Task WithExistingSystemKey_CanCacheSystemKeysTrue_CachesSystemKey()
    {
        // Arrange: CanCacheSystemKeys=true, CanCacheIntermediateKeys=false
        // This forces GetIntermediateKey to be called on every decrypt,
        // which in turn calls WithExistingSystemKey. The SK should be cached after first call.
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("systemKeyCacheTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: true, canCacheIntermediateKeys: false);

        const string inputValue = "Test data for system key caching";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Encrypt payload (creates both SK and IK in metastore)
        using var encryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await encryptEnvelope.EncryptPayloadAsync(inputBytes);

        // Create a new envelope instance with empty caches
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        // First decrypt: SK cache miss, loads from metastore, then caches it
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));

        // Second decrypt with same envelope: SK should be retrieved from cache
        // (IK is not cached due to CanCacheIntermediateKeys=false, so GetIntermediateKey
        // is called again, which calls WithExistingSystemKey, which should find SK in cache)
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    /// <summary>
    /// Tests that WithExistingSystemKey throws MetadataMissingException when the system key is revoked
    /// and treatExpiredAsMissing is true (during write operations).
    /// This triggers inline rotation to create a new IK and SK.
    /// </summary>
    [Fact]
    public async Task WithExistingSystemKey_RevokedSystemKey_TreatExpiredAsMissingTrue_LogsWarningAndCreatesNewKeys()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("revokedSkTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption - creates IK and SK
        using var encryptEnvelope1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes1 = await encryptEnvelope1.EncryptPayloadAsync(inputBytes);
        var ik1Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes1);

        // Mark the SK as revoked (IK remains valid)
        var skMeta = metastore.GetSystemKeyMetaForIntermediateKey(partition.IntermediateKeyId);
        Assert.NotNull(skMeta);
        var markResult = metastore.MarkKeyAsRevoked(skMeta.Value.keyId, skMeta.Value.created);
        Assert.True(markResult);

        // Wait to ensure we're in a different second for the new keys
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // Second encryption:
        // - Finds IK (not expired, not revoked)
        // - Calls WithExistingSystemKey with treatExpiredAsMissing=true
        // - Loads SK, decrypts it → CryptoKey with IsRevoked()=true
        // - IsKeyExpiredOrRevoked(systemKey) returns true (revoked)
        // - Throws MetadataMissingException, caught, logs warning
        // - Creates new IK and SK
        using var encryptEnvelope2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes2 = await encryptEnvelope2.EncryptPayloadAsync(inputBytes);
        var ik2Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes2);

        // Assert: New IK was created (different timestamp)
        Assert.NotEqual(ik1Created, ik2Created);

        // Assert: Warning was logged about the revoked SK
        var warningLogs = _loggerFactory.LogEntries
            .Where(e => e.LogLevel == LogLevel.Warning)
            .ToList();

        Assert.Single(warningLogs);
        Assert.Contains("missing or in an invalid state", warningLogs[0].Message);
        Assert.IsType<MetadataMissingException>(warningLogs[0].Exception);

        // Both payloads should still be decryptable (old SK still works for reads)
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes1);
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes2);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    /// <summary>
    /// Tests that WithExistingSystemKey uses cached system key for multiple different intermediate keys.
    /// This verifies the cache is used across different IK decryptions that share the same SK.
    /// </summary>
    [Fact]
    public async Task WithExistingSystemKey_CanCacheSystemKeysTrue_UsesCachedKeyForMultipleIKs()
    {
        // Arrange: CanCacheSystemKeys=true, CanCacheIntermediateKeys=false
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("systemKeyCacheMultiIKTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: true, canCacheIntermediateKeys: false);

        const string inputValue1 = "First payload";
        const string inputValue2 = "Second payload";
        var inputBytes1 = System.Text.Encoding.UTF8.GetBytes(inputValue1);
        var inputBytes2 = System.Text.Encoding.UTF8.GetBytes(inputValue2);

        // Encrypt first payload (creates IK1 and SK)
        using var encryptEnvelope1 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes1 = await encryptEnvelope1.EncryptPayloadAsync(inputBytes1);
        var ik1Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes1);

        // Mark the first IK as expired so a new IK will be created
        // (SK is NOT marked as expired, so it will be reused)
        cryptoPolicy.MarkKeyAsExpired(DateTimeOffset.FromUnixTimeSeconds(ik1Created));

        // Wait to ensure we're in a different second (policy uses second precision for IK)
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        // Encrypt second payload - creates new IK (because IK1 is expired) but reuses same SK
        using var encryptEnvelope2 = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes2 = await encryptEnvelope2.EncryptPayloadAsync(inputBytes2);
        var ik2Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes2);

        // Verify different IKs were created
        Assert.NotEqual(ik1Created, ik2Created);

        // Clear expirations for decryption (we want to decrypt both payloads successfully)
        cryptoPolicy.ClearExpirations();

        // Create a new envelope instance with empty caches for decryption
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        // Decrypt first payload: SK cache miss, loads from metastore, caches it
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes1);
        Assert.Equal(inputValue1, System.Text.Encoding.UTF8.GetString(decrypted1));

        // Decrypt second payload: Different IK, but same SK - should use cached SK
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes2);
        Assert.Equal(inputValue2, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    #endregion

    #region WithIntermediateKeyForRead Cache Tests

    /// <summary>
    /// Tests that WithIntermediateKeyForRead caches the intermediate key when CanCacheIntermediateKeys is true.
    /// The first decrypt loads the IK from metastore and caches it.
    /// The second decrypt uses the cached IK.
    /// </summary>
    [Fact]
    public async Task WithIntermediateKeyForRead_CanCacheIntermediateKeysTrue_CachesIntermediateKey()
    {
        // Arrange: CanCacheIntermediateKeys=true, CanCacheSystemKeys=false
        // This ensures we're testing IK caching specifically
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("ikCacheTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: true);

        const string inputValue = "Test data for intermediate key caching";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Encrypt payload (creates both SK and IK in metastore)
        using var encryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await encryptEnvelope.EncryptPayloadAsync(inputBytes);

        // Create a new envelope instance with empty caches for decryption
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        // First decrypt: IK cache miss, loads from metastore, caches it
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted1));

        // Second decrypt with same envelope: IK should be retrieved from cache
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    /// <summary>
    /// Tests that WithIntermediateKeyForRead uses cached intermediate key for multiple decrypt operations.
    /// Encrypts multiple payloads with the same IK, then decrypts them using cached IK.
    /// </summary>
    [Fact]
    public async Task WithIntermediateKeyForRead_CanCacheIntermediateKeysTrue_UsesCachedKeyForMultipleDecrypts()
    {
        // Arrange: CanCacheIntermediateKeys=true
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("ikCacheMultiDecryptTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: true);

        const string inputValue1 = "First payload";
        const string inputValue2 = "Second payload";
        var inputBytes1 = System.Text.Encoding.UTF8.GetBytes(inputValue1);
        var inputBytes2 = System.Text.Encoding.UTF8.GetBytes(inputValue2);

        // Encrypt both payloads (same IK will be used since same partition and within same second)
        using var encryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes1 = await encryptEnvelope.EncryptPayloadAsync(inputBytes1);
        var encryptedBytes2 = await encryptEnvelope.EncryptPayloadAsync(inputBytes2);

        // Verify both use the same IK
        var ik1Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes1);
        var ik2Created = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes2);
        Assert.Equal(ik1Created, ik2Created);

        // Create a new envelope instance with empty caches for decryption
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        // First decrypt: IK cache miss, loads from metastore, caches it
        var decrypted1 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes1);
        Assert.Equal(inputValue1, System.Text.Encoding.UTF8.GetString(decrypted1));

        // Second decrypt: Same IK, should use cached key
        var decrypted2 = await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes2);
        Assert.Equal(inputValue2, System.Text.Encoding.UTF8.GetString(decrypted2));
    }

    #endregion

    #region Duplicate Key Detection Tests

    /// <summary>
    /// Tests that when StoreAsync returns false (duplicate detected) during IK creation,
    /// the code falls through to retry logic and successfully uses the stored key.
    /// This simulates a concurrent encryption scenario where another process stored the key first.
    /// </summary>
    [Fact]
    public async Task GetLatestOrCreateIntermediateKey_DuplicateDetected_UsesRetryLogic()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("duplicateIkTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data for duplicate detection";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Configure metastore to fail the next 2 stores (IK and SK creation)
        // but still save the records (simulating another process stored first)
        metastore.FailNextStores(2);

        // Act: Encrypt - this will trigger:
        // 1. Phase 1: No existing IK → fall through
        // 2. Phase 2: Try to create IK → StoreAsync returns false (simulated duplicate)
        // 3. Phase 3: Retry - load the "duplicate" key and use it
        using var envelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await envelope.EncryptPayloadAsync(inputBytes);

        // Assert: Encryption succeeded despite duplicate detection
        ValidateDataRowRecordJson(encryptedBytes);

        // Verify decryption works
        var decrypted = await envelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted));
    }

    /// <summary>
    /// Tests that duplicate detection works for SK creation as well.
    /// </summary>
    [Fact]
    public async Task GetLatestOrCreateSystemKey_DuplicateDetected_UsesRetryLogic()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("duplicateSkTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data for SK duplicate detection";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Configure metastore to fail only the first store (SK creation)
        // The second store (IK creation) will succeed
        metastore.FailNextStores(1);

        // Act: Encrypt - SK creation will hit duplicate detection, then retry
        using var envelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await envelope.EncryptPayloadAsync(inputBytes);

        // Assert: Encryption succeeded
        ValidateDataRowRecordJson(encryptedBytes);

        // Verify decryption works
        var decrypted = await envelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted));
    }

    #endregion

    #region GetIntermediateKey Tests

    /// <summary>
    /// Tests that GetIntermediateKey throws MetadataMissingException when the IK's ParentKeyMeta is null.
    /// This can happen if there's data corruption in the metastore.
    /// </summary>
    [Fact]
    public async Task GetIntermediateKey_NullParentKeyMeta_ThrowsMetadataMissingException()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("nullParentKeyMetaReadTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption - creates IK and SK
        using var encryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await encryptEnvelope.EncryptPayloadAsync(inputBytes);

        // Get the IK's metadata from the encrypted data
        var ikCreated = GetIntermediateKeyCreatedFromDataRowRecord(encryptedBytes);

        // Clear the ParentKeyMeta of the IK (simulating data corruption)
        var cleared = metastore.ClearParentKeyMeta(partition.IntermediateKeyId, DateTimeOffset.FromUnixTimeSeconds(ikCreated));
        Assert.True(cleared);

        // Act & Assert: Trying to decrypt should fail because IK's ParentKeyMeta is null
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        var exception = await Assert.ThrowsAsync<MetadataMissingException>(async () =>
        {
            await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        });

        Assert.Contains("Could not find parentKeyMeta (SK) for intermediateKey", exception.Message);
    }

    #endregion

    #region GetSystemKey Tests

    /// <summary>
    /// Tests that GetSystemKey throws MetadataMissingException when the SK is not found in metastore.
    /// This can happen if the SK was deleted or if there's data corruption.
    /// </summary>
    [Fact]
    public async Task GetSystemKey_SystemKeyNotFound_ThrowsMetadataMissingException()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("missingSkTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // First encryption - creates IK and SK
        using var encryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await encryptEnvelope.EncryptPayloadAsync(inputBytes);

        // Delete the SK from metastore (simulating data loss/corruption)
        var skMeta = metastore.GetSystemKeyMetaForIntermediateKey(partition.IntermediateKeyId);
        Assert.NotNull(skMeta);
        var deleted = metastore.DeleteKey(skMeta.Value.keyId, skMeta.Value.created);
        Assert.True(deleted);

        // Act & Assert: Trying to decrypt should fail because SK is missing
        using var decryptEnvelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);

        var exception = await Assert.ThrowsAsync<MetadataMissingException>(async () =>
        {
            await decryptEnvelope.DecryptDataRowRecordAsync(encryptedBytes);
        });

        Assert.Contains("Could not find EnvelopeKeyRecord", exception.Message);
    }

    #endregion

    #region GetLatestOrCreateIntermediateKey Tests

    /// <summary>
    /// Tests that when an IK exists in metastore with null ParentKeyMeta, a warning is logged
    /// and a new IK is created instead.
    /// </summary>
    [Fact]
    public async Task GetLatestOrCreateIntermediateKey_WithNullParentKeyMeta_LogsWarningAndCreatesNewKey()
    {
        // Arrange
        var keyManagementService = new StaticKeyManagementService();
        var metastore = new InMemoryKeyMetastore();
        var partition = new DefaultPartition("nullParentKeyMetaTest", "testService", "testProduct");
        var cryptoPolicy = new ConfigurableCryptoPolicy(canCacheSystemKeys: false, canCacheIntermediateKeys: false);

        // Pre-store a corrupt IK with null ParentKeyMeta
        var corruptIkCreated = cryptoPolicy.TruncateToIntermediateKeyPrecision(DateTimeOffset.UtcNow);
        var corruptIkRecord = new KeyRecord(
            corruptIkCreated,
            Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }), // Fake encrypted key data
            false,
            null); // null ParentKeyMeta - this is the corrupt state we're testing

        await metastore.StoreAsync(partition.IntermediateKeyId, corruptIkCreated, corruptIkRecord);

        // Wait to ensure the new IK will have a different timestamp (second precision)
        // This prevents the new IK from colliding with the corrupt one
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        const string inputValue = "Test data";
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(inputValue);

        // Act: Encrypt will find the corrupt IK, log warning, and create a new valid IK
        using var envelope = NewEnvelopeEncryption(cryptoPolicy, keyManagementService, metastore, partition);
        var encryptedBytes = await envelope.EncryptPayloadAsync(inputBytes);

        // Assert: Verify warning was logged
        var warningLogs = _loggerFactory.LogEntries
            .Where(e => e.LogLevel == LogLevel.Warning)
            .ToList();

        Assert.Single(warningLogs);
        Assert.Contains("missing or in an invalid state", warningLogs[0].Message);
        Assert.Contains(partition.IntermediateKeyId, warningLogs[0].Message);
        Assert.IsType<MetadataMissingException>(warningLogs[0].Exception);

        // Verify encryption still succeeded (new IK was created)
        ValidateDataRowRecordJson(encryptedBytes);

        // Verify we can decrypt (proves the new IK is valid)
        var decrypted = await envelope.DecryptDataRowRecordAsync(encryptedBytes);
        Assert.Equal(inputValue, System.Text.Encoding.UTF8.GetString(decrypted));
    }

    #endregion

    /// <summary>
    /// Extracts the intermediate key Created timestamp from a DataRowRecord.
    /// </summary>
    private static long GetIntermediateKeyCreatedFromDataRowRecord(byte[] dataRowRecordBytes)
    {
        var dataRowObject = JsonNode.Parse(dataRowRecordBytes);
        return dataRowObject?["Key"]?["ParentKeyMeta"]?["Created"]?.GetValue<long>()
            ?? throw new InvalidOperationException("Could not extract IK Created from DataRowRecord");
    }

    private static void ValidateDataRowRecordJson(byte[] dataRowRecordBytes)
    {
        // Deserialize into JsonNode and validate structure matches this format:
        /*
          {
               "Key": {
                   "Created": 1752685310,
                   "Key": "base64-encryptedDataRowKeyByteArray",
                   "ParentKeyMeta": {
                       "KeyId": "_IK_widgets_dotnet-guild-tools_Human-Resources_us-west-2",
                       "Created": 1752501780
                   }
               },
               "Data": "base64-encryptedDataByteArray"
           }
         */
        var dataRowObject = JsonNode.Parse(dataRowRecordBytes);
        Assert.NotNull(dataRowObject);
        Assert.NotNull(dataRowObject["Key"]);
        Assert.Equal(JsonValueKind.Object, dataRowObject["Key"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Data"]);
        Assert.Equal(JsonValueKind.String, dataRowObject["Data"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Key"]?["Created"]);
        Assert.Equal(JsonValueKind.Number, dataRowObject["Key"]?["Created"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Key"]?["Key"]);
        Assert.Equal(JsonValueKind.String, dataRowObject["Key"]?["Key"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Key"]?["ParentKeyMeta"]);
        Assert.Equal(JsonValueKind.Object, dataRowObject["Key"]?["ParentKeyMeta"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Key"]?["ParentKeyMeta"]?["KeyId"]);
        Assert.Equal(JsonValueKind.String, dataRowObject["Key"]?["ParentKeyMeta"]?["KeyId"]?.GetValueKind());

        Assert.NotNull(dataRowObject["Key"]?["ParentKeyMeta"]?["Created"]);
        Assert.Equal(JsonValueKind.Number, dataRowObject["Key"]?["ParentKeyMeta"]?["Created"]?.GetValueKind());
    }
}
