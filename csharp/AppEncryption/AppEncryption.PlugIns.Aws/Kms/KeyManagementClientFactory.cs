using System;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Simple implementation of <see cref="IKeyManagementClientFactory"/> that creates KMS clients
    /// for any region using provided AWS credentials. Alternative implementations can be used
    /// if your application requires more complex credential management or client configuration.
    /// </summary>
    public class KeyManagementClientFactory : IKeyManagementClientFactory
    {
        private readonly AWSCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyManagementClientFactory"/> class.
        /// </summary>
        /// <param name="credentials">The AWS credentials to use for authentication.</param>
        public KeyManagementClientFactory(AWSCredentials credentials)
        {
            _credentials = credentials;
        }

        /// <inheritdoc/>
        public IAmazonKeyManagementService CreateForRegion(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new ArgumentException("Region cannot be null or empty", nameof(region));
            }

            // GetBySystemName will always return a RegionEndpoint. Sometimes with an invalid-name
            // but it could be working because AWS SDK matches to similar regions. So we don't
            // do any extra validation on the regionEndpoint here because the application intention
            // is not known
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);

            var config = new AmazonKeyManagementServiceConfig
            {
                RegionEndpoint = regionEndpoint
            };

            return new AmazonKeyManagementServiceClient(_credentials, config);
        }
    }
}
