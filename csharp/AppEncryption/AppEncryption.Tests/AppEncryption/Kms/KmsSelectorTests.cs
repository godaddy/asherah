using System;
using System.Diagnostics;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Kms
{
    public class KmsSelectorTests
    {
        [Fact]
        private void TestNull()
        {
            Assert.Throws<ArgumentNullException>(() => KeyManagementServiceSelector.SelectKmsWithConfiguration(null, null));
        }

        [Theory]
        [ClassData(typeof(TestKmsBadConfigurations))]
        private void Test(IConfiguration configuration, Type exceptionType)
        {
            Assert.Throws(
                exceptionType,
                () =>
                {
                    var cryptoPolicy = BasicExpiringCryptoPolicy.BuildWithConfiguration(configuration);
                    KeyManagementServiceSelector.SelectKmsWithConfiguration(cryptoPolicy, configuration);
                });
        }
    }
}
