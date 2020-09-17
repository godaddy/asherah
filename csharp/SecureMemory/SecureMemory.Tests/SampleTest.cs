using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using System;
using System.Diagnostics;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class SampleTest
    {
        [Fact]
        private void EndToEndTest()
        {
            Trace.Listeners.RemoveAt(0);
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            try
            {
                Debug.WriteLine("SampleTest.EndToEndTest");
                using (ISecretFactory secretFactory = new ProtectedMemorySecretFactory())
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
    }
}
