using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.Persistence;
using GoDaddy.Asherah.Logging;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Multithreaded
{
    [Collection("Configuration collection")]
    public class MultiPartitionMultiThreadedTest : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<MultiPartitionMultiThreadedTest>();
        private static readonly Persistence<byte[]> PersistenceBytes = PersistenceFactory<byte[]>.CreateInMemoryPersistence();

        private readonly SessionFactory sessionFactory;

        public MultiPartitionMultiThreadedTest(ConfigFixture configFixture)
        {
            sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService,
                configFixture.Metastore);
        }

        public void Dispose()
        {
            sessionFactory.Dispose();
        }

        /// <summary>
        /// Create a single session to encrypt and decrypt data for multiple partitions.
        /// Ensure keys get created properly, DRR's are created as expected,
        /// and (future) collect timing information to ensure no thread is
        /// starved more than others since there will be some locks in play.
        /// </summary>
        [Fact]
        private void MultiThreadedSameFactoryMultiplePartitionsEncryptDecrypt()
        {
            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumThreads), i =>
            {
                try
                {
                    RunEncryptDecryptTest(NumIterations, $"thread-pool-{i}", PayloadSizeBytes);
                    Interlocked.Increment(ref completedTasks);
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call");
                    throw;
                }
            });
            Assert.Equal(NumThreads, completedTasks);
        }

        private void RunEncryptDecryptTest(int testIterations, string partitionId, int payloadSizeBytesBase)
        {
            try
            {
                using (Session<JObject, byte[]> session =
                    sessionFactory.GetSessionJson(partitionId))
                {
                    Dictionary<string, byte[]> dataStore = new Dictionary<string, byte[]>();

                    string partitionPart = "partition-" + partitionId + "-";

                    for (int i = 0; i < testIterations; i++)
                    {
                        // Note the size will be slightly larger since we're adding extra unique meta
                        JObject jsonObject = PayloadGenerator.CreateRandomJsonPayload(payloadSizeBytesBase);
                        string keyPart = $"iteration-{i}";
                        jsonObject.Add("payload", partitionPart + keyPart);

                        dataStore.Add(keyPart, session.Encrypt(jsonObject));
                    }

                    foreach (KeyValuePair<string, byte[]> keyValuePair in dataStore)
                    {
                        JObject decryptedObject = session.Decrypt(keyValuePair.Value);
                        Assert.Equal(partitionPart + keyValuePair.Key, decryptedObject.GetValue("payload").ToObject<string>());
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "unexpected error during call");
                throw;
            }
        }

        /// <summary>
        /// Create a single session to load and store data for multiple partitions.
        /// Ensure keys get created properly, DRR's are created as expected,
        /// and (future) collect timing information to ensure no thread is
        /// starved more than others since there will be some locks in play.
        /// </summary>
        [Fact]
        private void MultiThreadedSameFactoryMultiplePartitionsLoadStore()
        {
            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumThreads), i =>
            {
                try
                {
                    RunLoadStoreTest(NumIterations, $"thread-pool-{i}", PayloadSizeBytes);
                    Interlocked.Increment(ref completedTasks);
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call");
                    throw;
                }
            });
            Assert.Equal(NumThreads, completedTasks);
        }

        private void RunLoadStoreTest(int testIterations, string partitionId, int payloadSizeBytesBase)
        {
            try
            {
                using (Session<JObject, byte[]> session =
                    sessionFactory.GetSessionJson(partitionId))
                {
                    string partitionPart = "partition-" + partitionId + "-";

                    for (int i = 0; i < testIterations; i++)
                    {
                        // Note the size will be slightly larger since we're adding extra unique meta
                        JObject jsonObject = PayloadGenerator.CreateRandomJsonPayload(payloadSizeBytesBase);
                        string keyPart = $"iteration-{i}";
                        jsonObject.Add("payload", partitionPart + keyPart);

                        string persistenceKey = session.Store(jsonObject, PersistenceBytes);
                        Option<JObject> decryptedJsonPayload = session.Load(persistenceKey, PersistenceBytes);
                        if (decryptedJsonPayload.IsSome)
                        {
                            JObject decryptedJson = (JObject)decryptedJsonPayload;
                            Assert.Equal(partitionPart + keyPart, decryptedJson.GetValue("payload").ToObject<string>());
                        }
                        else
                        {
                            throw new XunitException("Json load did not return decrypted payload");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "unexpected error during call");
                throw;
            }
        }
    }
}
