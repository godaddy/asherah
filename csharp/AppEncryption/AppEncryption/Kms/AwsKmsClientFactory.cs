using Amazon;
using Amazon.KeyManagementService;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    public class AwsKmsClientFactory
    {
        internal virtual IAmazonKeyManagementService CreateAwsKmsClient(string region)
        {
            // TODO Replace with call that takes region as string and avoid instance resolution if SDK ever adds it
            return new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(region));
        }
    }
}
