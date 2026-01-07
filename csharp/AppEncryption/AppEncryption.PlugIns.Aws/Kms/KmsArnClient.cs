using Amazon.KeyManagementService;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Internal class that holds a KMS ARN and its corresponding client.
    /// </summary>
    internal sealed class KmsArnClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KmsArnClient"/> class.
        /// </summary>
        /// <param name="arn">The KMS key ARN.</param>
        /// <param name="client">The Amazon KMS client.</param>
        /// <param name="region">The AWS region.</param>
        public KmsArnClient(string arn, IAmazonKeyManagementService client, string region)
        {
            Arn = arn;
            Client = client;
            Region = region;
        }

        /// <summary>
        /// Gets the KMS key ARN.
        /// </summary>
        public string Arn { get; }

        /// <summary>
        /// Gets the Amazon KMS client.
        /// </summary>
        public IAmazonKeyManagementService Client { get; }

        /// <summary>
        /// Gets the AWS region.
        /// </summary>
        public string Region { get; }
    }
}
