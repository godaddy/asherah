using System.Collections.Generic;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    internal sealed class KeyManagementServiceBuilder : IKeyManagementServiceBuilder
    {
        private ILoggerFactory _loggerFactory;
        private AWSCredentials _credentials;
        private IKeyManagementClientFactory _kmsClientFactory;
        private KeyManagementServiceOptions _kmsOptions;
        private readonly List<RegionKeyArn> _regionKeyArns = new List<RegionKeyArn>(4);

        public IKeyManagementServiceBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        public IKeyManagementServiceBuilder WithCredentials(AWSCredentials credentials)
        {
            _credentials = credentials;
            return this;
        }

        public IKeyManagementServiceBuilder WithKmsClientFactory(IKeyManagementClientFactory kmsClientFactory)
        {
            _kmsClientFactory = kmsClientFactory;
            return this;
        }

        public IKeyManagementServiceBuilder WithRegionKeyArn(string region, string keyArn)
        {
            if (string.IsNullOrEmpty(region))
            {
                throw new System.ArgumentException("Region cannot be null or empty", nameof(region));
            }

            if (string.IsNullOrEmpty(keyArn))
            {
                throw new System.ArgumentException("Key ARN cannot be null or empty", nameof(keyArn));
            }

            _regionKeyArns.Add(new RegionKeyArn { Region = region, KeyArn = keyArn });
            return this;
        }

        public IKeyManagementServiceBuilder WithOptions(KeyManagementServiceOptions options)
        {
            _kmsOptions = options;
            return this;
        }

        public KeyManagementService Build()
        {
            if (_loggerFactory == null)
            {
                throw new System.InvalidOperationException("LoggerFactory must be provided");
            }

            var resolvedOptions = ResolveOptions();
            var resolvedClientFactory = ResolveClientFactory();

            return new KeyManagementService(resolvedOptions, resolvedClientFactory, _loggerFactory);
        }

        private IKeyManagementClientFactory ResolveClientFactory()
        {
            if (_kmsClientFactory != null)
            {
                return _kmsClientFactory;
            }

            return _credentials == null
                ? throw new System.InvalidOperationException("Either credentials or a KMS client factory must be provided")
                : new KeyManagementClientFactory(_credentials);
        }

        private KeyManagementServiceOptions ResolveOptions()
        {
            if (_kmsOptions != null)
            {
                return _kmsOptions;
            }

            if (_regionKeyArns.Count == 0)
            {
                throw new System.InvalidOperationException("At least one region and key ARN pair must be provided if not using WithOptions");
            }

            return new KeyManagementServiceOptions
            {
                RegionKeyArns = _regionKeyArns
            };
        }
    }
}
