using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using GoDaddy.Asherah.AppEncryption.Kms;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Stub implementation of IKeyManagementClientFactory for testing purposes.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="KeyManagementClientFactoryStub"/> class.
    /// </remarks>
    /// <param name="options">The key management service options.</param>
    [ExcludeFromCodeCoverage]
    public class KeyManagementClientFactoryStub(KeyManagementServiceOptions options) : AwsKmsClientFactory, IKeyManagementClientFactory
    {
        private readonly Dictionary<string, AwsKeyManagementStub> _clients = [];

        /// <inheritdoc/>
        public IAmazonKeyManagementService CreateForRegion(string region)
        {
            if (_clients.TryGetValue(region, out var existingClient))
            {
                return existingClient;
            }

            var regionKeyArn = options.RegionKeyArns.FirstOrDefault(rka => rka.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                               ?? throw new InvalidOperationException($"No key ARN found for region: {region}");

            var client = new AwsKeyManagementStub(regionKeyArn.KeyArn);
            _clients[region] = client;
            return client;
        }

        internal override IAmazonKeyManagementService CreateAwsKmsClient(string region, AWSCredentials credentials)
        {
            return CreateForRegion(region);
        }
    }
}
