using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Meter;
using GoDaddy.Asherah.AppEncryption.Envelope;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.AppEncryption.Util;
using GoDaddy.Asherah.Crypto;
using GoDaddy.Asherah.Crypto.Keys;
using Microsoft.Extensions.Caching.Memory;
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

        private Mock<InMemoryMetastoreImpl<JObject>> metastoreSpy;

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
        private void TestSessionCacheSetupAndClose()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            MemoryCache sessionCacheManager;
            using (SessionFactory factory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                policy,
                keyManagementServiceMock.Object))
            {
                sessionCacheManager = factory.SessionCacheManager;
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes("1234"))
                {
                    Assert.NotNull(sessionBytes);
                }

                // Verify nothing evicted yet
                Assert.True(factory.SessionCacheManager.Count > 0);
            }

            // Verify closing the factory invalidated and cleaned up entries
            Assert.True(sessionCacheManager.Count == 0);
        }

        [Fact]
        private void TestSessionCacheGetSessionWhileStillUsedAndNotExpiredShouldNotEvict()
        {
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                    byte[] drr = sessionBytes.Encrypt(payload);

                    // Reset so we can examine 2nd session's interactions
                    metastoreSpy.Reset();

                    // Use same partition to get the same cached session while it's still in use
                    using (Session<byte[], byte[]> session2 = factory.GetSessionBytes(TestPartitionId))
                    {
                        byte[] decryptedPayload = session2.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);

                        // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
                        metastoreSpy.Verify(
                            x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
                    }
                }
            }
        }

        [Fact]
        private void TestSessionCacheGetSessionWhileStillUsedAndExpiredShouldNotEvict()
        {
            long sessionCacheExpireMillis = 30;
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                    byte[] drr = sessionBytes.Encrypt(payload);

                    // Sleep to trigger the expiration
                    try
                    {
                        Thread.Sleep((int)sessionCacheExpireMillis * 3);
                    }
                    catch (Exception e)
                    {
                        Assert.True(false, e.Message);
                    }

                    // Even after timeout, verify that we have one entry in cache
                    Assert.Equal(1, factory.SessionCacheManager.Count);

                    // Reset so we can examine 2nd session's interactions
                    metastoreSpy.Reset();

                    // Use same partition to get the same cached session while it's still in use
                    using (Session<byte[], byte[]> session2 = factory.GetSessionBytes(TestPartitionId))
                    {
                        byte[] decryptedPayload = session2.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);

                        // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
                        metastoreSpy.Verify(
                            x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
                    }
                }
            }
        }

        [Fact]
        private void TestSessionCacheGetSessionAfterUseAndNotExpiredShouldNotEvict()
        {
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                byte[] drr = null;

                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    drr = sessionBytes.Encrypt(payload);
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                    Assert.Equal(payload, decryptedPayload);
                }

                // Note we do not sleep

                // Reset so we can examine 2nd session's interactions
                metastoreSpy.Reset();

                // Use same partition to get the same cached session while it's still in use
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                    Assert.Equal(payload, decryptedPayload);

                    // verify no metastore interactions in the decrypt flow (since IKs cached via session caching)
                    metastoreSpy.Verify(
                        x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
                }
            }
        }

        [Fact]
        private void TestSessionCacheGetSessionAfterUseAndExpiredShouldEvict()
        {
            long sessionCacheExpireMillis = 10;
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                byte[] drr = null;
                Partition partition = factory.GetPartition(TestPartitionId);
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    drr = sessionBytes.Encrypt(payload);
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                    Assert.Equal(payload, decryptedPayload);
                }

                // Sleep to trigger the expiration
                try
                {
                    Thread.Sleep((int)sessionCacheExpireMillis * 3);
                }
                catch (Exception e)
                {
                    Assert.True(false, e.Message);
                }

                // Reset so we can examine 2nd session's interactions
                metastoreSpy.Reset();

                // This will actually create a new session and the previous one will be removed/closed due to expiry
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);
                    Assert.Equal(payload, decryptedPayload);

                    // metastore should have an interaction in the decrypt flow since the cached session expire
                    metastoreSpy.Verify(
                x => x.Load(partition.IntermediateKeyId, It.IsAny<DateTimeOffset>()));
                }
            }
        }

        [Fact]
        private void TestSessionCacheMultiThreadedSameSessionNoEviction()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                int numThreads = 100;
                int numRequests = 100;

                // Get the current settings and try to force minWorkers
                ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                long completedTasks = 0;
                Parallel.ForEach(Enumerable.Range(0, numRequests), i =>
                {
                    using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                    {
                        byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                        byte[] drr = sessionBytes.Encrypt(payload);
                        byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);
                        Interlocked.Increment(ref completedTasks);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that cache has only 1 entry
                Assert.Equal(1, factory.SessionCacheManager.Count);
            }
        }

        [Fact]
        private void TestSessionCacheMultiThreadedDifferentSessionsNoEviction()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                int numThreads = 100;
                int numRequests = 100;

                // Get the current settings and try to force minWorkers
                ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                long completedTasks = 0;
                Parallel.ForEach(Enumerable.Range(0, numRequests), i =>
                {
                    using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId + i))
                    {
                        byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                        byte[] drr = sessionBytes.Encrypt(payload);
                        byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);
                        Interlocked.Increment(ref completedTasks);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that number of entries in cache equal number of partitions
                Assert.Equal(numRequests, factory.SessionCacheManager.Count);
            }
        }

        [Fact]
        private void TestSessionCacheMultiThreadedWithExpirationSameSession()
        {
            long sessionCacheExpireMillis = 10;
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                int numThreads = 100;
                int numRequests = 100;

                // Get the current settings and try to force minWorkers
                ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                long completedTasks = 0;
                Parallel.ForEach(Enumerable.Range(0, numRequests), i =>
                {
                    using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                    {
                        byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                        byte[] drr = sessionBytes.Encrypt(payload);
                        byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);
                        Interlocked.Increment(ref completedTasks);

                        Thread.Sleep((int)sessionCacheExpireMillis * 3);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that cache has only 1 entry
                Assert.Equal(1, factory.SessionCacheManager.Count);
            }
        }

        [Fact]
        private void TestSessionCacheMultiThreadedWithExpirationDifferentSessions()
        {
            long sessionCacheExpireMillis = 10;
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestMasterKey)
                .Build())
            {
                int numThreads = 100;
                int numRequests = 100;

                // Get the current settings and try to force minWorkers
                ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                long completedTasks = 0;
                Parallel.ForEach(Enumerable.Range(0, numRequests), i =>
                {
                    using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId + i))
                    {
                        byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                        byte[] drr = sessionBytes.Encrypt(payload);
                        byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                        Assert.Equal(payload, decryptedPayload);
                        Interlocked.Increment(ref completedTasks);

                        Thread.Sleep((int)sessionCacheExpireMillis * 3);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that number of entries in cache equal number of partitions
                Assert.Equal(numRequests, factory.SessionCacheManager.Count);
            }
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
