using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Multithreaded
{
    public class MultiFactoryThreadedTest
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<MultiFactoryThreadedTest>();

        // Create multiple sessions from multiple factories
        // to encrypt and decrypt data for multiple partitions.
        // Ensure keys get created properly, DRR's are created as expected,
        // and (future) collect timing information to ensure no thread is
        // starved more than others since there will be some locks in play.
        [Fact]
        public void MultiThreadedMultiFactoryUniquePartitionsEncryptDecrypt()
        {
            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumRequests), i =>
            {
                try
                {
                    RunPartitionTest(NumIterations, $"request-{i}", PayloadSizeBytes);
                    Interlocked.Increment(ref completedTasks);
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call: ");
                    throw;
                }
            });

            // Wait for all threads to complete
            Assert.Equal(NumRequests, completedTasks);
        }

        // Using the same partition id, create multiple sessions
        // from multiple factories to encrypt and decrypt data.
        // Ensure keys get created properly, DRR's are created as expected,
        // and (future) collect timing information to ensure no thread is
        // starved more than others since there will be some locks in play.
        [Fact]
        public void MultiThreadedMultiFactorySamePartitionEncryptDecrypt()
        {
            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumRequests), i =>
            {
                try
                {
                    RunPartitionTest(NumIterations, DefaultPartitionId, PayloadSizeBytes);
                    Interlocked.Increment(ref completedTasks);
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call: ");
                    throw;
                }
            });

            // Wait for all threads to complete
            Assert.Equal(NumRequests, completedTasks);
        }

        private void RunPartitionTest(int testIterations, string partitionId, int payloadSizeBytesBase)
        {
            try
            {
                using (AppEncryptionSessionFactory factory =
                    SessionFactoryGenerator.CreateDefaultAppEncryptionSessionFactory())
                {
                    using (AppEncryption<JObject, byte[]> partition = factory.GetAppEncryptionJson(partitionId))
                    {
                        Dictionary<string, byte[]> dataStore = new Dictionary<string, byte[]>();

                        string partitionPart = $"partition-{partitionId}-";

                        for (int i = 0; i < testIterations; i++)
                        {
                            // Note the size will be slightly larger since we're adding extra unique meta
                            JObject jObject = PayloadGenerator.CreateRandomJsonPayload(payloadSizeBytesBase);
                            string keyPart = $"iteration-{i}";
                            jObject["payload"] = partitionPart + keyPart;

                            dataStore.Add(keyPart, partition.Encrypt(jObject));
                        }

                        foreach (KeyValuePair<string, byte[]> keyValuePair in dataStore)
                        {
                            JObject decryptedObject = partition.Decrypt(keyValuePair.Value);
                            Assert.Equal(partitionPart + keyValuePair.Key, decryptedObject["payload"].ToObject<string>());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected error during call");
                throw;
            }
        }
    }
}
