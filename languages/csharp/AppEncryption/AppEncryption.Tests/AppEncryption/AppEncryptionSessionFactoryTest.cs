using System;
using App.Metrics;
using App.Metrics.Meter;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class AppEncryptionSessionFactoryTest
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestSystemId = "test_system_id";
        private const string TestProductId = "test_product_id";
        private const string TestMasterKey = "test_master_key";

        private readonly Mock<IMetastorePersistence<JObject>> metastorePersistenceMock;
        private readonly Mock<CryptoPolicy> cryptoPolicyMock;
        private readonly Mock<KeyManagementService> keyManagementServiceMock;
        private readonly Mock<SecureCryptoKeyDictionaryFactory<DateTimeOffset>> secureCryptoKeyDictionaryFactoryMock;
        private readonly Mock<SecureCryptoKeyDictionary<DateTimeOffset>> systemKeyCacheMock;

        private readonly AppEncryptionSessionFactory appEncryptionSessionFactory;

        public AppEncryptionSessionFactoryTest()
        {
            metastorePersistenceMock = new Mock<IMetastorePersistence<JObject>>();
            cryptoPolicyMock = new Mock<CryptoPolicy>();
            keyManagementServiceMock = new Mock<KeyManagementService>();
            secureCryptoKeyDictionaryFactoryMock =
                new Mock<SecureCryptoKeyDictionaryFactory<DateTimeOffset>>(cryptoPolicyMock.Object);
            systemKeyCacheMock = new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(1);
            secureCryptoKeyDictionaryFactoryMock.Setup(x => x.CreateSecureCryptoKeyDictionary())
                .Returns(systemKeyCacheMock.Object);

            appEncryptionSessionFactory = new AppEncryptionSessionFactory(
                TestProductId,
                TestSystemId,
                metastorePersistenceMock.Object,
                secureCryptoKeyDictionaryFactoryMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object);
        }

        [Fact]
        private void TestConstructor()
        {
            AppEncryptionSessionFactory appEncryptionSessionFactory = new AppEncryptionSessionFactory(
                TestProductId,
                TestSystemId,
                metastorePersistenceMock.Object,
                secureCryptoKeyDictionaryFactoryMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object);

            Assert.NotNull(appEncryptionSessionFactory);
        }

        [Fact]
        private void TestGetAppEncryptionJson()
        {
            AppEncryption<JObject, byte[]> appEncryptionJson =
                appEncryptionSessionFactory.GetAppEncryptionJson(TestPartitionId);
            Assert.NotNull(appEncryptionJson);
        }

        [Fact]
        private void TestGetAppEncryptionBytes()
        {
            AppEncryption<byte[], byte[]> appEncryptionBytes =
                appEncryptionSessionFactory.GetAppEncryptionBytes(TestPartitionId);
            Assert.NotNull(appEncryptionBytes);
        }

        [Fact]
        private void TestGetAppEncryptionJsonAsJson()
        {
            AppEncryption<JObject, JObject> appEncryption =
                appEncryptionSessionFactory.GetAppEncryptionJsonAsJson(TestPartitionId);
            Assert.NotNull(appEncryption);
        }

        [Fact]
        private void TestGetAppEncryptionBytesAsJson()
        {
            AppEncryption<byte[], JObject> appEncryption =
                appEncryptionSessionFactory.GetAppEncryptionBytesAsJson(TestPartitionId);
            Assert.NotNull(appEncryption);
        }

        [Fact]
        private void TestGetEnvelopeEncryptionBytes()
        {
            IEnvelopeEncryption<byte[]> appEncryption =
                appEncryptionSessionFactory.GetEnvelopeEncryptionBytes(TestPartitionId);
            Assert.NotNull(appEncryption);
        }

        [Fact]
        private void TestGetAppEncryptionPartitionWithPartition()
        {
            AppEncryptionPartition appEncryptionPartition =
                appEncryptionSessionFactory.GetAppEncryptionPartition(TestPartitionId);

            Assert.Equal(TestPartitionId, appEncryptionPartition.PartitionId);
            Assert.Equal(TestSystemId, appEncryptionPartition.SystemId);
            Assert.Equal(TestProductId, appEncryptionPartition.ProductId);
        }

        [Fact]
        private void TestDisposeSuccess()
        {
            appEncryptionSessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestDisposeWithDisposeFailShouldReturn()
        {
            systemKeyCacheMock.Setup(x => x.Dispose()).Throws<SystemException>();
            appEncryptionSessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestBuilderPathWithPrebuiltInterfaces()
        {
            AppEncryptionSessionFactory.IMetastoreStep metastoreStep =
                AppEncryptionSessionFactory.NewBuilder(TestProductId, TestSystemId);
            Assert.NotNull(metastoreStep);

            AppEncryptionSessionFactory.ICryptoPolicyStep cryptoPolicyStep = metastoreStep.WithMemoryPersistence();
            Assert.NotNull(cryptoPolicyStep);

            AppEncryptionSessionFactory.IKeyManagementServiceStep keyManagementServiceStep =
                cryptoPolicyStep.WithNeverExpiredCryptoPolicy();
            Assert.NotNull(keyManagementServiceStep);

            AppEncryptionSessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithStaticKeyManagementService(TestMasterKey);
            Assert.NotNull(buildStep);

            AppEncryptionSessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        private void TestBuilderPathWithSpecifiedInterfaces()
        {
            AppEncryptionSessionFactory.IMetastoreStep metastoreStep =
                AppEncryptionSessionFactory.NewBuilder(TestProductId, TestSystemId);
            Assert.NotNull(metastoreStep);

            IMetastorePersistence<JObject> metastorePersistence = new MemoryPersistenceImpl<JObject>();
            AppEncryptionSessionFactory.ICryptoPolicyStep cryptoPolicyStep =
                metastoreStep.WithMetaStorePersistence(metastorePersistence);
            Assert.NotNull(cryptoPolicyStep);

            CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
            AppEncryptionSessionFactory.IKeyManagementServiceStep keyManagementServiceStep =
                cryptoPolicyStep.WithCryptoPolicy(cryptoPolicy);
            Assert.NotNull(keyManagementServiceStep);

            KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(TestMasterKey);
            AppEncryptionSessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithKeyManagementService(keyManagementService);
            Assert.NotNull(buildStep);

            AppEncryptionSessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        private void TestBuilderPathWithMetricsDisabled()
        {
            AppEncryptionSessionFactory.NewBuilder(TestProductId, TestSystemId)
                .WithMemoryPersistence()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestMasterKey)
                .Build();

            MetricsUtil.MetricsInstance.Measure.Meter.Mark(new MeterOptions { Name = "should.not.record" }, 1);

            // Verify no metrics were recorded
            Assert.Empty(MetricsUtil.MetricsInstance.Snapshot.Get().Contexts);
        }

        [Fact]
        private void TestBuilderPathWithMetricsEnabled()
        {
            IMetrics metrics = new MetricsBuilder().Build();
            AppEncryptionSessionFactory.NewBuilder(TestProductId, TestSystemId)
                .WithMemoryPersistence()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestMasterKey)
                .WithMetrics(metrics)
                .Build();

            MetricsUtil.MetricsInstance.Measure.Meter.Mark(new MeterOptions { Name = "should.record" }, 1);

            // Verify metrics were recorded
            Assert.NotEmpty(MetricsUtil.MetricsInstance.Snapshot.Get().Contexts);
        }
    }
}
