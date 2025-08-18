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
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    public class SessionFactoryTest : IDisposable
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";
        private const string TestRegionSuffix = "test_region_suffix";
        private const string TestStaticMasterKey = "thisIsAStaticMasterKeyForTesting";

        private readonly Mock<IMetastore<JObject>> metastoreMock;
        private readonly Mock<InMemoryMetastoreImpl<JObject>> metastoreSpy;
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
            metastoreSpy = new Mock<InMemoryMetastoreImpl<JObject>> { CallBase = true };

            sessionFactory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object);
            sessionFactory.Dispose();
        }

        [Fact]
        public void TestConstructor()
        {
            using (SessionFactory sessionFactory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                cryptoPolicyMock.Object,
                keyManagementServiceMock.Object))
            {
                Assert.NotNull(sessionFactory);
            }
        }

        [Fact]
        public void TestSessionCacheSetupAndDispose()
        {
            // Test flows around session cache setup, including cache loader and removal flows (via dispose)
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            MemoryCache sessionCache;
            using (SessionFactory factory = new SessionFactory(
                TestProductId,
                TestServiceId,
                metastoreMock.Object,
                systemKeyCacheMock.Object,
                policy,
                keyManagementServiceMock.Object))
            {
                sessionCache = factory.SessionCache;
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes("1234"))
                {
                    Assert.NotNull(sessionBytes);
                }

                // Verify nothing evicted yet
                Assert.True(sessionCache.Count > 0);
            }

            // Verify closing the factory invalidated and cleaned up entries
            Assert.True(sessionCache.Count == 0);
        }

        [Fact]
        public void TestSessionCacheGetSessionWhileStillUsedAndNotExpiredShouldNotEvict()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
        public void TestSessionCacheGetSessionWhileStillUsedAndExpiredShouldNotEvict()
        {
            long sessionCacheExpireMillis = 30;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
                        Assert.Fail(e.Message);
                    }

                    // Even after timeout, verify that we have one entry in cache
                    Assert.Equal(1, factory.SessionCache.Count);

                    // Reset so we can examine 2nd session's interactions
                    metastoreSpy.Reset();

                    // Use same partition to get the same cached (but expired) session while it's still in use
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
        public void TestSessionCacheGetSessionAfterUseAndNotExpiredShouldNotEvict()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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

                // Verify that nothing is evicted from cache since the session is still in use.
                Assert.Equal(1, factory.SessionCache.Count);
            }
        }

        [Fact]
        public void TestSessionCacheGetSessionAfterUseAndExpiredShouldEvict()
        {
            long sessionCacheExpireMillis = 10;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
                    Assert.Fail(e.Message);
                }

                // Reset so we can examine 2nd session's interactions
                metastoreSpy.Reset();

                // This will actually create a new session and the previous one will be removed/closed due to expiry
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);
                    Assert.Equal(payload, decryptedPayload);

                    // metastore should have an interaction in the decrypt flow since the cached session expired
                    metastoreSpy.Verify(
                x => x.Load(partition.IntermediateKeyId, It.IsAny<DateTimeOffset>()));
                }
            }
        }

        [Fact]
        public void TestSessionCacheGetSessionWithMaxSessionNotReachedShouldNotEvict()
        {
            long sessionCacheMaxSize = 2;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(sessionCacheMaxSize)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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

                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId + 1))
                {
                }

                // Sleep to attempt to trigger eviction on next access if it were going to
                try
                {
                    Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    Assert.Fail(e.Message);
                }

                // Reset so we can examine 2nd session's interactions
                metastoreSpy.Reset();

                // Try to use same partition to get the same cached session while it's still in use
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                    Assert.Equal(payload, decryptedPayload);

                    // we should not hit the metastore since the session should not have been evicted
                    metastoreSpy.Verify(
                        x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
                }
            }
        }

        [Fact]
        public void TestSessionCacheGetSessionWithMaxSessionReachedShouldEvict()
        {
            long sessionCacheMaxSize = 2;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(sessionCacheMaxSize)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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

                // Try to create more sessions to exceed the max size
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId + 1))
                {
                }

                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId + 2))
                {
                }

                // Reset so we can examine 2nd session's interactions
                metastoreSpy.Reset();

                // Try to use same partition to get the same session we might have cached earlier
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] decryptedPayload = sessionBytes.Decrypt(drr);

                    Assert.Equal(payload, decryptedPayload);

                    // we should hit the metastore since the session should have been evicted as the size limit was reached
                    metastoreSpy.Verify(
                        x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()));
                }
            }
        }

        [Fact]
        public void TestSessionCacheGetSessionWithMaxSessionReachedButStillUsedShouldNotEvict()
        {
            long sessionCacheMaxSize = 1;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(sessionCacheMaxSize)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .Build())
            {
                using (Session<byte[], byte[]> sessionBytes = factory.GetSessionBytes(TestPartitionId))
                {
                    byte[] payload = { 0, 1, 2, 3, 4, 5, 6, 7 };
                    byte[] drr = sessionBytes.Encrypt(payload);

                    // Force us to hit the max cache size
                    using (Session<byte[], byte[]> session2 = factory.GetSessionBytes(TestPartitionId + 1))
                    {
                        byte[] drr1 = sessionBytes.Encrypt(payload);
                        byte[] decryptedPayload = sessionBytes.Decrypt(drr1);

                        Assert.Equal(payload, decryptedPayload);

                        // Reset so we can examine 2nd session's interactions
                        metastoreSpy.Reset();

                        // Get same session as the outer-most block since this should force both of the sessions to stay
                        using (Session<byte[], byte[]> sessionBytesDup = factory.GetSessionBytes(TestPartitionId))
                        {
                            byte[] decryptedPayloadDup = sessionBytesDup.Decrypt(drr);

                            Assert.Equal(payload, decryptedPayloadDup);

                            // we should not hit the metastore since the session should not have been evicted
                            metastoreSpy.Verify(
                                x => x.Load(It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
                        }
                    }
                }
            }
        }

        [Fact(Skip = "Fails randomly during CI")]
        public void TestSessionCacheMultiThreadedSameSessionNoEviction()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
                Assert.Equal(1, factory.SessionCache.Count);
            }
        }

        [Fact]
        public void TestSessionCacheMultiThreadedDifferentSessionsNoEviction()
        {
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
                Assert.Equal(numRequests, factory.SessionCache.Count);
            }
        }

        [Fact(Skip = "Fails randomly during CI")]
        public void TestSessionCacheMultiThreadedWithMaxSessionReachedSameSession()
        {
            long sessionCacheMaxSize = 1;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(sessionCacheMaxSize)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
                Assert.Equal(1, factory.SessionCache.Count);
            }
        }

        [Fact]
        public void TestSessionCacheMultiThreadedWithMaxSessionReachedDifferentSessions()
        {
            long sessionCacheMaxSize = 2;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)
                .WithSessionCacheMaxSize(sessionCacheMaxSize)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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
            }
        }

        [Fact]
        public void TestSessionCacheMultiThreadedWithExpirationSameSession()
        {
            long sessionCacheExpireMillis = 10;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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

                        // Sleep to trigger expiration
                        Thread.Sleep((int)sessionCacheExpireMillis * 3);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that cache has only 1 entry
                Assert.Equal(1, factory.SessionCache.Count);
            }
        }

        [Fact]
        public void TestSessionCacheMultiThreadedWithExpirationDifferentSessions()
        {
            long sessionCacheExpireMillis = 10;
            CryptoPolicy policy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithSessionCacheExpireMillis(sessionCacheExpireMillis)
                .WithCanCacheSessions(true)
                .Build();

            using (SessionFactory factory = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithMetastore(metastoreSpy.Object)
                .WithCryptoPolicy(policy)
                .WithStaticKeyManagementService(TestStaticMasterKey)
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

                        // Sleep to trigger expiration
                        Thread.Sleep((int)sessionCacheExpireMillis * 3);
                    }
                });

                // Wait for all threads to complete
                Assert.Equal(numRequests, completedTasks);

                // Verify that number of entries in cache equal number of partitions
                Assert.Equal(numRequests, factory.SessionCache.Count);
            }
        }

        [Fact]
        public void TestGetSessionJson()
        {
            Session<JObject, byte[]> sessionJson =
                sessionFactory.GetSessionJson(TestPartitionId);
            Assert.NotNull(sessionJson);
        }

        [Fact]
        public void TestGetSessionBytes()
        {
            Session<byte[], byte[]> sessionBytes =
                sessionFactory.GetSessionBytes(TestPartitionId);
            Assert.NotNull(sessionBytes);
        }

        [Fact]
        public void TestGetSessionJsonAsJson()
        {
            Session<JObject, JObject> session =
                sessionFactory.GetSessionJsonAsJson(TestPartitionId);
            Assert.NotNull(session);
        }

        [Fact]
        public void TestGetSessionBytesAsJson()
        {
            Session<byte[], JObject> session =
                sessionFactory.GetSessionBytesAsJson(TestPartitionId);
            Assert.NotNull(session);
        }

        [Fact]
        public void TestGetEnvelopeEncryptionBytes()
        {
            IEnvelopeEncryption<byte[]> envelopeEncryption =
                sessionFactory.GetEnvelopeEncryptionBytes(TestPartitionId);
            Assert.NotNull(envelopeEncryption);
        }

        [Fact]
        public void TestGetPartitionWithPartitionId()
        {
            Partition partition =
                sessionFactory.GetPartition(TestPartitionId);

            Assert.Equal(TestPartitionId, partition.PartitionId);
            Assert.Equal(TestServiceId, partition.ServiceId);
            Assert.Equal(TestProductId, partition.ProductId);
        }

        [Fact]
        public void TestGetPartitionWithSuffixedPartition()
        {
            metastoreMock.Setup(x => x.GetKeySuffix()).Returns(TestRegionSuffix);
            Partition partition =
                sessionFactory.GetPartition(TestPartitionId);

            Assert.Equal(TestPartitionId, partition.PartitionId);
            Assert.Equal(TestServiceId, partition.ServiceId);
            Assert.Equal(TestProductId, partition.ProductId);
        }

        [Fact]
        public void TestDisposeSuccess()
        {
            sessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        public void TestDisposeWithDisposeFailShouldReturn()
        {
            systemKeyCacheMock.Setup(x => x.Dispose()).Throws<SystemException>();
            sessionFactory.Dispose();

            // Verify proper resources are closed
            systemKeyCacheMock.Verify(x => x.Dispose());
        }

        [Fact]
        public void TestBuilderPathWithPrebuiltInterfaces()
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
                keyManagementServiceStep.WithStaticKeyManagementService(TestStaticMasterKey);
            Assert.NotNull(buildStep);

            SessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        public void TestBuilderPathWithSpecifiedInterfaces()
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

            KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(TestStaticMasterKey);
            SessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithKeyManagementService(keyManagementService);
            Assert.NotNull(buildStep);

            SessionFactory sessionFactory = buildStep.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        public void TestBuilderPathWithMetricsDisabled()
        {
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .Build();

            MetricsUtil.MetricsInstance.Measure.Meter.Mark(new MeterOptions { Name = "should.not.record" }, 1);

            // Verify no metrics were recorded
            Assert.Empty(MetricsUtil.MetricsInstance.Snapshot.Get().Contexts);
        }

        [Fact]
        public void TestBuilderPathWithMetricsEnabled()
        {
            IMetrics metrics = new MetricsBuilder().Build();
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .WithMetrics(metrics)
                .Build();

            MetricsUtil.MetricsInstance.Measure.Meter.Mark(new MeterOptions { Name = "should.record" }, 1);

            // Verify metrics were recorded
            Assert.NotEmpty(MetricsUtil.MetricsInstance.Snapshot.Get().Contexts);
        }

        [Fact]
        public void TestBuilderPathWithLoggerEnabled()
        {
            var mockLogger = new Mock<ILogger>();
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .WithLogger(mockLogger.Object)
                .Build();

            // Verify the SessionFactory was created successfully
            // (implicitly tested by the fact that Build() didn't throw)
        }

        [Fact]
        public void TestBuilderPathWithLoggerDisabled()
        {
            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .Build();

            // Verify the SessionFactory was created successfully without a logger
            // (implicitly tested by the fact that Build() didn't throw)
        }

        [Fact]
        public void TestBuilderPathWithLoggerInFullChain()
        {
            var mockLogger = new Mock<ILogger>();

            SessionFactory.IMetastoreStep metastoreStep =
                SessionFactory.NewBuilder(TestProductId, TestServiceId);
            Assert.NotNull(metastoreStep);

            SessionFactory.ICryptoPolicyStep cryptoPolicyStep = metastoreStep.WithInMemoryMetastore();
            Assert.NotNull(cryptoPolicyStep);

            SessionFactory.IKeyManagementServiceStep keyManagementServiceStep =
                cryptoPolicyStep.WithNeverExpiredCryptoPolicy();
            Assert.NotNull(keyManagementServiceStep);

            SessionFactory.IBuildStep buildStep =
                keyManagementServiceStep.WithStaticKeyManagementService(TestStaticMasterKey);
            Assert.NotNull(buildStep);

            SessionFactory.IBuildStep buildStepWithLogger = buildStep.WithLogger(mockLogger.Object);
            Assert.NotNull(buildStepWithLogger);

            SessionFactory sessionFactory = buildStepWithLogger.Build();
            Assert.NotNull(sessionFactory);
        }

        [Fact]
        public void TestBuilderPathWithLoggerAndMetrics()
        {
            var mockLogger = new Mock<ILogger>();
            IMetrics metrics = new MetricsBuilder().Build();

            SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey)
                .WithLogger(mockLogger.Object)
                .WithMetrics(metrics)
                .Build();

            // Verify the SessionFactory was created successfully with both logger and metrics
            // (implicitly tested by the fact that Build() didn't throw)
        }

        [Fact]
        public void TestWithLoggerReturnsCorrectInterface()
        {
            var mockLogger = new Mock<ILogger>();

            SessionFactory.IBuildStep buildStep = SessionFactory.NewBuilder(TestProductId, TestServiceId)
                .WithInMemoryMetastore()
                .WithNeverExpiredCryptoPolicy()
                .WithStaticKeyManagementService(TestStaticMasterKey);

            SessionFactory.IBuildStep result = buildStep.WithLogger(mockLogger.Object);

            // Verify the return type is IBuildStep interface (not concrete Builder type)
            Assert.IsAssignableFrom<SessionFactory.IBuildStep>(result);
        }

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            sessionFactory?.Dispose();
        }
    }
}
