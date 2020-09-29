using System;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class MetastoreSelectorTests
    {
        [Theory]
        [ClassData(typeof(TestGoodConfigurations))]
        private void TestLoadAndStoreWithValidKey(IConfiguration configuration)
        {
            MetastoreSelector<JObject>.SelectMetastoreWithConfiguration(configuration);
        }

        [Fact]
        private void TestNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                MetastoreSelector<JObject>.SelectMetastoreWithConfiguration(null);
            });
        }

        [Theory]
        [ClassData(typeof(TestMetastoreBadConfigurations))]
        private void TestMetastoreBadConfigurations(IConfiguration configuration, Type exceptionType)
        {
            Assert.Throws(exceptionType, () =>
            {
                MetastoreSelector<JObject>.SelectMetastoreWithConfiguration(configuration);
            });
        }
    }
}
