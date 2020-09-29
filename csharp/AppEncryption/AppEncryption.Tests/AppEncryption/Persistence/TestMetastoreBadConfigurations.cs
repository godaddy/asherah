using System;
using System.Collections.Generic;
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class TestMetastoreBadConfigurations : TheoryData<IConfiguration, Type>
    {
        public TestMetastoreBadConfigurations()
        {
            Add(null, typeof(ArgumentNullException));
            Add(TestAdoNoConnectionString(), typeof(AppEncryptionException));
            Add(TestAdoNoFactoryType(), typeof(AppEncryptionException));
            Add(TestMissingDynamoDbRegion(), typeof(AppEncryptionException));
            Add(TestBadMetastoreType(), typeof(AppEncryptionException));
        }

        private IConfiguration TestAdoNoConnectionString()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "metastoreType", "ado" },
                }).Build();
        }

        private IConfiguration TestAdoNoFactoryType()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "metastoreType", "ado" },
                    { "metastoreAdoConnectionString", "host=foo" },
                }).Build();
        }

        private IConfiguration TestMissingDynamoDbRegion()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "metastoreType", "dynamodb" },
                }).Build();
        }

        private IConfiguration TestBadMetastoreType()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    { "metastoreType", "blackhole-for-metadata" },
                }).Build();
        }
    }
}
