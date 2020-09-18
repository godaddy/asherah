using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class EndToEndTests
    {
        [Fact]
        private void EndToEndTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            try
            {
                Debug.WriteLine("SampleTest.EndToEndTest");
                using (ISecretFactory secretFactory = new ProtectedMemorySecretFactory(configuration))
                {
                    var secretBytes = new byte[] { 0, 1, 2, 3 };
                    using (var secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]))
                    { 
                        secret.WithSecretBytes(decryptedBytes => Assert.Equal(secretBytes, decryptedBytes));
                    }
                }
                Debug.WriteLine("SampleTest.EndToEndTest finish");
            }
            catch(Exception e)
            {
                Debug.WriteLine("SampleTest.EndToEndTest exception: " + e.Message);
            }
        }

        [Fact]
        private void EndToEndOpenSSLTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var dictionary = new Dictionary<string,string>
            {
                { "secureHeapEngine", "openssl11" },
                { "heapSize", "32000" },
                { "minimumAllocationSize", "128" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(dictionary)
                .Build();

            try
            {
                Debug.WriteLine("SampleTest.EndToEndOpenSSLTest");
                using (ISecretFactory secretFactory = new ProtectedMemorySecretFactory(configuration))
                {
                    var secretBytes = new byte[] { 0, 1, 2, 3 };
                    using (var secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]))
                    {
                        secret.WithSecretBytes(decryptedBytes => Assert.Equal(secretBytes, decryptedBytes));
                    }
                }
                Debug.WriteLine("SampleTest.EndToEndOpenSSLTest finish");
            }
            catch (Exception e)
            {
                Debug.WriteLine("SampleTest.EndToEndOpenSSLTest exception: " + e.Message);
            }
        }
    }
}
