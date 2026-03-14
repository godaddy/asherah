using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Kms;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Core
{
    public class SessionFactoryTests
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";

        private readonly LoggerFactoryStub _loggerFactory = new();

        private static GoDaddy.Asherah.AppEncryption.Core.SessionFactory NewSessionFactory(
            InMemoryKeyMetastore metastore,
            BasicExpiringCryptoPolicy cryptoPolicy,
            IKeyManagementService keyManagementService,
            ILogger logger)
        {
            return GoDaddy.Asherah.AppEncryption.Core.SessionFactory
                .NewBuilder(TestProductId, TestServiceId)
                .WithKeyMetastore(metastore)
                .WithCryptoPolicy(cryptoPolicy)
                .WithKeyManagementService(keyManagementService)
                .WithLogger(logger)
                .Build();
        }

        private GoDaddy.Asherah.AppEncryption.Core.SessionFactory NewSessionFactory(
            bool canCacheSessions = false)
        {
            var metastore = new InMemoryKeyMetastore();
            var keyManagementService = new StaticKeyManagementService();
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheIntermediateKeys(true)
                .WithCanCacheSystemKeys(true)
                .WithCanCacheSessions(canCacheSessions)
                .Build();
            var logger = _loggerFactory.CreateLogger(nameof(SessionFactoryTests));
            return NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger);
        }

        [Fact]
        public void GetSession_ReturnsNonNullSession()
        {
            using var factory = NewSessionFactory();
            using var session = factory.GetSession(TestPartitionId);

            Assert.NotNull(session);
        }

        [Fact]
        public void EncryptDecrypt_WithDefaults_Sync()
        {
            const string inputValue = "The quick brown fox jumps over the lazy dog";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);

            using var factory = NewSessionFactory();
            using var session = factory.GetSession(TestPartitionId);
            var dataRowRecordBytes = session.Encrypt(inputBytes);

            ValidateDataRowRecordJson(dataRowRecordBytes);

            var decryptedBytes = session.Decrypt(dataRowRecordBytes);
            var outputValue = Encoding.UTF8.GetString(decryptedBytes);

            Assert.Equal(inputValue, outputValue);
        }

        [Fact]
        public async Task EncryptDecrypt_WithDefaults_Async()
        {
            const string inputValue = "The quick brown fox jumps over the lazy dog";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);

            using var factory = NewSessionFactory();
            using var session = factory.GetSession(TestPartitionId);
            var dataRowRecordBytes = await session.EncryptAsync(inputBytes);

            ValidateDataRowRecordJson(dataRowRecordBytes);

            var decryptedBytes = await session.DecryptAsync(dataRowRecordBytes);
            var outputValue = Encoding.UTF8.GetString(decryptedBytes);

            Assert.Equal(inputValue, outputValue);
        }

        [Fact]
        public async Task EncryptDecrypt_MultipleTimes_WithDefaults()
        {
            const string inputValue = "The quick brown fox jumps over the lazy dog";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);
            const string inputValue2 = "Lorem ipsum dolor sit amet";
            var inputBytes2 = Encoding.UTF8.GetBytes(inputValue2);

            using var factory = NewSessionFactory();
            using var session = factory.GetSession(TestPartitionId);
            var dataRowRecordBytes = await session.EncryptAsync(inputBytes);
            var dataRowRecordBytes2 = await session.EncryptAsync(inputBytes2);

            ValidateDataRowRecordJson(dataRowRecordBytes);
            ValidateDataRowRecordJson(dataRowRecordBytes2);

            var decryptedBytes = await session.DecryptAsync(dataRowRecordBytes);
            Assert.Equal(inputValue, Encoding.UTF8.GetString(decryptedBytes));

            var decryptedBytes2 = await session.DecryptAsync(dataRowRecordBytes2);
            Assert.Equal(inputValue2, Encoding.UTF8.GetString(decryptedBytes2));
        }

        [Fact]
        public async Task EncryptDecrypt_WithDifferentInstances_SameMetastoreAndKms()
        {
            var keyManagementService = new StaticKeyManagementService();
            var metastore = new InMemoryKeyMetastore();
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheIntermediateKeys(true)
                .WithCanCacheSystemKeys(true)
                .WithCanCacheSessions(false)
                .Build();
            var logger = _loggerFactory.CreateLogger(nameof(SessionFactoryTests));

            const string inputValue = "shared metastore test";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);

            byte[] dataRowRecordBytes;
            using (var factory1 = NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger))
            using (var session1 = factory1.GetSession(TestPartitionId))
            {
                dataRowRecordBytes = await session1.EncryptAsync(inputBytes);
                ValidateDataRowRecordJson(dataRowRecordBytes);
            }

            using (var factory2 = NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger))
            using (var session2 = factory2.GetSession(TestPartitionId))
            {
                var decryptedBytes = await session2.DecryptAsync(dataRowRecordBytes);
                Assert.Equal(inputValue, Encoding.UTF8.GetString(decryptedBytes));
            }

            keyManagementService.Dispose();
            metastore.Dispose();
        }

        [Fact]
        public void GetSession_DifferentPartitionIds_ReturnSessionsThatEncryptIndependently()
        {
            const string inputValue = "partition isolation";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);

            using var factory = NewSessionFactory();
            using var sessionA = factory.GetSession("partitionA");
            using var sessionB = factory.GetSession("partitionB");

            var encryptedA = sessionA.Encrypt(inputBytes);
            var encryptedB = sessionB.Encrypt(inputBytes);

            ValidateDataRowRecordJson(encryptedA);
            ValidateDataRowRecordJson(encryptedB);

            Assert.Equal(inputValue, Encoding.UTF8.GetString(sessionA.Decrypt(encryptedA)));
            Assert.Equal(inputValue, Encoding.UTF8.GetString(sessionB.Decrypt(encryptedB)));
        }

        [Fact]
        public void GetSession_WhenCachingEnabled_SamePartitionReturnsCachedSession()
        {
            using var metastore = new InMemoryKeyMetastore();
            using var keyManagementService = new StaticKeyManagementService();
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheIntermediateKeys(true)
                .WithCanCacheSystemKeys(true)
                .WithCanCacheSessions(true)
                .Build();
            var logger = _loggerFactory.CreateLogger(nameof(SessionFactoryTests));

            const string inputValue = "cached session test";
            var inputBytes = Encoding.UTF8.GetBytes(inputValue);

            using (var factory = NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger))
            {
                byte[] encrypted;
                using (var session1 = factory.GetSession(TestPartitionId))
                {
                    encrypted = session1.Encrypt(inputBytes);
                    ValidateDataRowRecordJson(encrypted);
                }

                using (var session2 = factory.GetSession(TestPartitionId))
                {
                    var decrypted = session2.Decrypt(encrypted);
                    Assert.Equal(inputValue, Encoding.UTF8.GetString(decrypted));
                }
            }
        }

        [Fact]
        public void Dispose_AfterGetSession_DoesNotThrow()
        {
            using var metastore = new InMemoryKeyMetastore();
            using var keyManagementService = new StaticKeyManagementService();
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(false)
                .Build();
            var logger = _loggerFactory.CreateLogger(nameof(SessionFactoryTests));

            var factory = NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger);
            using (var session = factory.GetSession(TestPartitionId))
            {
                _ = session.Encrypt(Encoding.UTF8.GetBytes("dispose test"));
            }

            factory.Dispose();
        }

        [Fact]
        public void Dispose_WithoutAnyOperations_DoesNotThrow()
        {
            using var metastore = new InMemoryKeyMetastore();
            using var keyManagementService = new StaticKeyManagementService();
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(false)
                .Build();
            var logger = _loggerFactory.CreateLogger(nameof(SessionFactoryTests));

            var factory = NewSessionFactory(metastore, cryptoPolicy, keyManagementService, logger);
            factory.Dispose();
        }

        private static void ValidateDataRowRecordJson(byte[] dataRowRecordBytes)
        {
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
        }
    }
}
