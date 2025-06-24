using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    /// <summary>
    /// A factory to create an AWS KMS client based on the region provided.
    /// </summary>
    public class AwsKmsClientFactory
    {
        internal virtual IAmazonKeyManagementService CreateAwsKmsClient(string region, AWSCredentials credentials)
        {
            // TODO Replace with call that takes region as string and avoid instance resolution if SDK ever adds it
            return new AmazonKeyManagementServiceClient(credentials, RegionEndpoint.GetBySystemName(region));
        }
    }
}
