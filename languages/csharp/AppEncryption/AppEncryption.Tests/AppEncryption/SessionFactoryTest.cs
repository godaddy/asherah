using System;
using System.Threading;
using App.Metrics;
using App.Metrics.Meter;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Exceptions;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class SessionFactoryTest
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";
        private const string TestMasterKey = "test_master_key_that_is_32_bytes";

        private readonly Mock<IMetastore<JObject>> metastoreMock;
        private readonly Mock<CryptoPolicy> cryptoPolicyMock;
        private readonly Mock<KeyManagementService> keyManagementServiceMock;
        private readonly Mock<SecureCryptoKeyDictionary<DateTimeOffset>> systemKeyCacheMock;

        private readonly SessionFactory sessionFactory;

        public SessionFactoryTest()
        {
            metastoreMock = new Mock<IMetastore<JObject>>();
            cryptoPolicyMock = new Mock<CryptoPolicy>();
            keyManagementServiceMock = new Mock<KeyManagementService>();
            systemKeyCacheMock = new Mock<SecureCryptoKeyDictionary<DateTimeOffset>>(1);

            sessionFactory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object);
        }

        [Fact]
        private void TestConstructor()
        {
            SessionFactory sessionFactory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object);

            Assert.NotNull(sessionFactory);
        }

        [Fact]
        private void TestSessionCacheSetup()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            SessionFactory sess = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                policy,
                keyManagementServiceMock.Object);

            Session<byte[], byte[]> sessionBytes = sess.GetSessionBytes("1234");

            Assert.NotNull(sessionBytes);
        }

        [Fact]
        private void TestSessionCacheIsUsed()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheExpireMinutes(1)
                .Build();

            SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build();

            Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId);

            byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };

            byte[] drr = sessionBytes.Encrypt(payload);

            Session<byte[], byte[]> session2 = factory.GetSessionBytes(TestPartitionId);
            byte[] decryptedPayload = session2.Decrypt(drr);

            Assert.Equal(payload, decryptedPayload);
        }

        [Fact]
        private void TestSessionCacheAfterExpiryShouldFail()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheExpireMinutes(1)
                .Build();

            SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build();

            Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId);

            byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };

            byte[] drr = sessionBytes.Encrypt(payload);

            Session<byte[], byte[]> session2 = factory.GetSessionBytes(TestPartitionId);
            byte[] decryptedPayload = session2.Decrypt(drr);

            Assert.Equal(payload, decryptedPayload);

            session2.Dispose();
            sessionBytes.Dispose();
            Thread.Sleep(70000);

            Session<byte[], byte[]> session3 = factory.GetSessionBytes(TestPartitionId);

            Assert.Throws<AppEncryptionException>(() => session3.Decrypt(drr));
        }

        [Fact]
        private void TestGetSessionJson()
        {
            Session<JObject, byte[]> sessionJson =
                sessionFactory.GetSessionJson(TestPartitionId);
            Assert.NotNull(sessionJson);
        }

        [Fact]
        private void TestGetSessionBytes()
        {
            Session<byte[], byte[]> sessionBytes =
                sessionFactory.GetSessionBytes(TestPartitionId);
            Assert.NotNull(sessionBytes);
        }

        [Fact]
        private void TestGetSessionJsonAsJson()
        {
            Session<JObject, JObject> session =
                sessionFactory.GetSessionJsonAsJson(TestPartitionId);
            Assert.NotNull(session);
        }

        [Fact]
        private void TestGetSessionBytesAsJson()
        {
            Session<byte[], JObject> session =
                sessionFactory.GetSessionBytesAsJson(TestPartitionId);
            Assert.NotNull(session);
        }

        [Fact]
        private void TestGetEnvelopeEncryptionBytes()
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption =
                sessionFactory.GetEnvelopeEncryptionBytes(TestPartitionId);
            Assert.NotNull(envelopeEncryption);
        }

        [Fact]
        private void TestGetPartitionWithPartitionId()
        {
            Partition partition =
                sessionFactory.GetPartition(TestPartitionId);

            Assert.Equal(TestPartitionId, partition.PartitionId);
            Assert.Equal(TestServiceId, partition.ServiceId);
            Assert.Equal(TestProductId, partition.ProductId);
        }

        [Fact]
        private void TestDisposeSuccess()
        {
            sessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestDisposeWithDisposeFailShouldReturn()
        {
            systemKeyCacheMock.Setup(x => x.Dispose()).Throws<SystemException>();
            sessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestBuilderPathWithPrebuiltInterfaces()
        {
            SessionFactory.IMetastoreStep metastoreStep =
                SessionFactory.NewBuilder(TestProductId, TestServiceId);
            Assert.NotNull(metastoreStep);

            SessionFactory.ICryptoPolicyStep cryptoPolicyStep = metastoreStep.WithInMemoryMetastore();
            Assert.NotNull(cryptoPolicyStep);

            SessionFactory.IKeyManagementServiceStep keyManagementServiceStep =
                cryptoPolicyStep.WithNeverExpiredCryptoPolicy();
            Assert.NotNull(keyManagementServiceStep);

            SessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithStaticKeyManagementService(TestMasterKey);
            Assert.NotNull(buildStep);

            SessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        private void TestBuilderPathWithSpecifiedInterfaces()
        {
            SessionFactory.IMetastoreStep metastoreStep =
                SessionFactory.NewBuilder(TestProductId, TestServiceId);
            Assert.NotNull(metastoreStep);

            IMetastore<JObject> metastore = new InMemoryMetastoreImpl<JObject>();
            SessionFactory.ICryptoPolicyStep cryptoPolicyStep =
                metastoreStep.WithMetastore(metastore);
            Assert.NotNull(cryptoPolicyStep);

            CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
            SessionFactory.IKeyManagementServiceStep keyManagementServiceStep =
                cryptoPolicyStep.WithCryptoPolicy(cryptoPolicy);
            Assert.NotNull(keyManagementServiceStep);

            KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(TestMasterKey);
            SessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithKeyManagementService(keyManagementService);
            Assert.NotNull(buildStep);

            SessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        private void TestBuilderPathWithMetricsDisabled()
        {
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
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
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
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
