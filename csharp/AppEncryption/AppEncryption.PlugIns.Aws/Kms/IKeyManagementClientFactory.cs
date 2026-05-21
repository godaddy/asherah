using Amazon.KeyManagementService;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Factory interface for creating AWS KMS clients for specific regions.
    /// </summary>
    public interface IKeyManagementClientFactory
    {
        /// <summary>
        /// Creates a KMS client for the specified region.
        /// </summary>
        /// <param name="region">The AWS region name.</param>
        /// <returns>A KMS client configured for the specified region.</returns>
        IAmazonKeyManagementService CreateForRegion(string region);
    }
}
