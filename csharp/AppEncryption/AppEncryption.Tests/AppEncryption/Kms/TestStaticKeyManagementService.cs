using System;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Kms
{
    public class TestStaticKeyManagementService
    {
        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void TestStatic(IConfiguration configuration)
        {
            var cryptoPolicy = BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(TimeSpan.MaxValue.Days)
                .WithRevokeCheckMinutes((int)TimeSpan.MaxValue.TotalMinutes)
                .WithConfiguration(configuration)
                .Build();
            using var staticKms = new StaticKeyManagementServiceImpl("StaticKey", cryptoPolicy, configuration);
        }
    }
}
