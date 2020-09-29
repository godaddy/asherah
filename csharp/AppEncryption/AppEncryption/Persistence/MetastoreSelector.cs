using System;
#if !NETSTANDARD2_0
using System.Data.Common;
#endif
using GoDaddy.Asherah.Crypto.Exceptions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    public class MetastoreSelector<T>
    {
        public const string MetastoreType = "metastoreType";
        public const string MetastoreAdoConnectionString = "metastoreAdoConnectionString";
        public const string MetastoreAdo = "ado";
        public const string MetastoreAdoFactoryType = "metastoreAdoFactoryType";
        public const string MetastoreDynamoDb = "dynamodb";
        public const string MetastoreDynamoDbRegion = "metastoreDynamodbRegion";
        public const string MetastoreMemory = "memory";
        public const string DefaultMetastoreType = MetastoreMemory;

        public static IMetastore<T> SelectMetastoreWithConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var metastoreType = configuration[MetastoreType];
            if (string.IsNullOrWhiteSpace(metastoreType))
            {
                metastoreType = DefaultMetastoreType;
            }

            if (metastoreType.Equals(MetastoreAdo, StringComparison.InvariantCultureIgnoreCase))
            {
#if !NETSTANDARD2_0
                string metastoreAdoConnectionString = configuration[MetastoreAdoConnectionString];

                if (string.IsNullOrWhiteSpace(metastoreAdoConnectionString))
                {
                    throw new AppEncryptionException("Missing metastoreAdoConnectionString");
                }

                string metastoreAdoFactoryType = configuration[MetastoreAdoFactoryType];

                if (string.IsNullOrWhiteSpace(metastoreAdoFactoryType))
                {
                    throw new AppEncryptionException("Missing metastoreAdoFactoryType");
                }

                return (IMetastore<T>)AdoMetastoreImpl
                    .NewBuilder(DbProviderFactories.GetFactory(metastoreAdoFactoryType), metastoreAdoConnectionString)
                    .Build();
#else
                // .NET Standard 2.0 does not include DbProviderFactories
                throw new NotSupportedException("Metastore ADO cannot be setup by configuration in .NET Standard 2.0");
#endif
            }

            if (metastoreType.Equals(MetastoreDynamoDb, StringComparison.InvariantCultureIgnoreCase))
            {
                string metastoreDynamoDbRegion = configuration[MetastoreDynamoDbRegion];

                if (string.IsNullOrWhiteSpace(metastoreDynamoDbRegion))
                {
                    throw new AppEncryptionException("Missing metastoreDynamodbRegion");
                }

                return (IMetastore<T>)DynamoDbMetastoreImpl.NewBuilder(metastoreDynamoDbRegion).Build();
            }

            if (metastoreType.Equals(MetastoreMemory, StringComparison.InvariantCultureIgnoreCase))
            {
                return (IMetastore<T>)new InMemoryMetastoreImpl<JObject>();
            }

            throw new AppEncryptionException("Invalid metastore type: " + metastoreType);
        }
    }
}
